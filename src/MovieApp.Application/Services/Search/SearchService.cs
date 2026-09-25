using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Common;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class SearchService(
    ISearchRepository searchRepository,
    ISearchHistoryRepository searchHistoryRepository,
    ISearchProviderRefreshRepository refreshRepository,
    ICurrentUser currentUser,
    ICacheService cacheService,
    IUnifiedSearchProviderIngestionService providerIngestionService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    ISearchRefreshLockService refreshLockService,
    ISearchRefreshCompletionSignal refreshCompletionSignal,
    IOptions<SearchOptions> searchOptions,
    ILogger<SearchService> logger,
    IServiceScopeFactory? searchHistoryScopeFactory = null) : ISearchService
{
    private static readonly TimeSpan MinimumRefreshWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshWaitSafetyMargin = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefreshPollInterval = TimeSpan.FromMilliseconds(200);

    public async Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedSearchValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var options = searchOptions.Value;
        var cacheKey = UnifiedSearchCacheKeys.Create(criteria, contentLocale);
        var cachedEntry = await cacheService.GetAsync<UnifiedSearchCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            SearchServiceLogMessages.LogCacheHit(logger, criteria.Query, criteria.Type, criteria.Page);
            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return cachedEntry.Result;
        }

        if (!UnifiedSearchProviderPolicy.IsProviderScope(criteria))
        {
            var dbResult = await searchRepository.SearchAsync(criteria, cancellationToken);
            var localizedDbResult = await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
                dbResult,
                contentLocale,
                cancellationToken);

            if (UnifiedSearchProviderPolicy.ShouldCacheDbResult(localizedDbResult))
            {
                await cacheService.SetAsync(
                    cacheKey,
                    new UnifiedSearchCacheEntry { Result = localizedDbResult },
                    options.CacheDuration,
                    cancellationToken);
            }

            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return localizedDbResult;
        }

        var lockKey = SearchRefreshLockKeys.Create(criteria);
        var lockHandle = await refreshLockService.TryAcquireAsync(
            lockKey,
            options.ProviderRefreshLockDuration,
            cancellationToken);

        if (lockHandle is null)
        {
            var waiterResult = await WaitForConcurrentProviderSearchAsync(
                lockKey,
                cacheKey,
                criteria,
                contentLocale,
                options,
                cancellationToken);

            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return waiterResult;
        }

        SearchRefreshAttemptOutcome? refreshOutcome = null;
        Exception? terminalException = null;
        PaginatedResult<SearchItem>? result = null;

        try
        {
            using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ownershipLost = false;
            var renewalTask = SearchRefreshLockRenewal.RunAsync(
                refreshLockService,
                lockHandle,
                options,
                () =>
                {
                    ownershipLost = true;
                    refreshCancellation.Cancel();
                },
                refreshCancellation.Token);

            try
            {
                result = await ExecuteProviderSearchAsync(
                    criteria,
                    contentLocale,
                    cacheKey,
                    options,
                    refreshCancellation.Token);

                if (ownershipLost)
                {
                    refreshOutcome = SearchRefreshAttemptOutcome.Failed;
                    terminalException = new SearchProviderUnavailableException();
                }
                else
                {
                    refreshOutcome = SearchRefreshAttemptOutcome.Succeeded;
                }
            }
            catch (SearchProviderUnavailableException exception)
            {
                refreshOutcome = SearchRefreshAttemptOutcome.Failed;
                terminalException = exception;
            }
            catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                refreshOutcome = SearchRefreshAttemptOutcome.Failed;
                terminalException = new SearchProviderUnavailableException();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                refreshOutcome = SearchRefreshAttemptOutcome.Failed;
                throw;
            }
            finally
            {
                await refreshCancellation.CancelAsync();
                try
                {
                    await renewalTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            if (refreshOutcome.HasValue)
            {
                await refreshCompletionSignal.PublishAsync(
                    lockKey,
                    refreshOutcome.Value,
                    ResolveCompletionSignalTtl(options),
                    cancellationToken);
            }

            await refreshLockService.ReleaseAsync(
                lockHandle.LockKey,
                lockHandle.LockToken,
                lockHandle.Backend,
                cancellationToken);
        }

        if (terminalException is not null)
        {
            throw terminalException;
        }

        await TryRecordSearchHistoryAsync(criteria, cancellationToken);
        return result!;
    }

    private async Task<PaginatedResult<SearchItem>> ExecuteProviderSearchAsync(
        SearchCriteria criteria,
        string contentLocale,
        string cacheKey,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        UnifiedSearchProviderIngestionResult ingestionResult;

        try
        {
            ingestionResult = await providerIngestionService.IngestAsync(criteria, contentLocale, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return await FallbackToDatabaseAsync(criteria, contentLocale, cancellationToken);
        }

        if (!ingestionResult.IsFullySuccessful || ingestionResult.Result is null)
        {
            return await FallbackToDatabaseAsync(criteria, contentLocale, cancellationToken);
        }

        var normalizedQuery = GetNormalizedQuery(criteria);
        if (normalizedQuery is not null)
        {
            await refreshRepository.SetLastRefreshedAtUtcAsync(
                normalizedQuery,
                criteria.Type,
                criteria.Page,
                DateTime.UtcNow,
                cancellationToken);
        }

        await cacheService.SetAsync(
            cacheKey,
            new UnifiedSearchCacheEntry { Result = ingestionResult.Result },
            options.CacheDuration,
            cancellationToken);

        return ingestionResult.Result;
    }

    private async Task<PaginatedResult<SearchItem>> FallbackToDatabaseAsync(
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var dbResult = await searchRepository.SearchAsync(criteria, cancellationToken);
        var localizedDbResult = await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
            dbResult,
            contentLocale,
            cancellationToken);

        if (localizedDbResult.TotalCount > 0)
        {
            SearchServiceLogMessages.LogDbFallback(logger, criteria.Query, criteria.Type, criteria.Page);
            return localizedDbResult;
        }

        SearchServiceLogMessages.LogProviderUnavailable(logger, criteria.Query, criteria.Type, criteria.Page);
        throw new SearchProviderUnavailableException();
    }

    private async Task<PaginatedResult<SearchItem>> WaitForConcurrentProviderSearchAsync(
        string lockKey,
        string cacheKey,
        SearchCriteria criteria,
        string contentLocale,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ResolveRefreshWaitDuration(options);

        while (DateTime.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cachedResult = await TryGetCachedResultAsync(cacheKey, cancellationToken);
            if (cachedResult is not null)
            {
                return cachedResult;
            }

            var completionOutcome = await refreshCompletionSignal.TryGetOutcomeAsync(lockKey, cancellationToken);
            if (completionOutcome == SearchRefreshAttemptOutcome.Failed)
            {
                return await FallbackToDatabaseAsync(criteria, contentLocale, cancellationToken);
            }

            await Task.Delay(RefreshPollInterval, cancellationToken);
        }

        var finalCachedResult = await TryGetCachedResultAsync(cacheKey, cancellationToken);
        if (finalCachedResult is not null)
        {
            return finalCachedResult;
        }

        var finalOutcome = await refreshCompletionSignal.TryGetOutcomeAsync(lockKey, cancellationToken);
        if (finalOutcome == SearchRefreshAttemptOutcome.Failed)
        {
            return await FallbackToDatabaseAsync(criteria, contentLocale, cancellationToken);
        }

        if (finalOutcome == SearchRefreshAttemptOutcome.Succeeded)
        {
            var completedCachedResult = await TryGetCachedResultAsync(cacheKey, cancellationToken);
            if (completedCachedResult is not null)
            {
                return completedCachedResult;
            }
        }

        return await FallbackToDatabaseAsync(criteria, contentLocale, cancellationToken);
    }

    private async Task<PaginatedResult<SearchItem>?> TryGetCachedResultAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var cachedEntry = await cacheService.GetAsync<UnifiedSearchCacheEntry>(cacheKey, cancellationToken);
        return cachedEntry?.Result;
    }

    private static string? GetNormalizedQuery(SearchCriteria criteria) =>
        string.IsNullOrWhiteSpace(criteria.Query)
            ? null
            : QueryNormalizer.Normalize(criteria.Query);

    private static TimeSpan ResolveRefreshWaitDuration(SearchOptions options)
    {
        var waitDuration = options.ProviderRefreshLockDuration - RefreshWaitSafetyMargin;
        return waitDuration < MinimumRefreshWait ? MinimumRefreshWait : waitDuration;
    }

    private static TimeSpan ResolveCompletionSignalTtl(SearchOptions options) =>
        options.ProviderRefreshLockDuration + RefreshWaitSafetyMargin;

    private async Task TryRecordSearchHistoryAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(criteria.Query) || !currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return;
        }

        var queryValidation = AdvancedSearchValidator.ValidateQuery(criteria.Query);
        if (!queryValidation.IsValid)
        {
            return;
        }

        var normalizedQuery = QueryNormalizer.Normalize(criteria.Query);
        var displayQuery = QueryNormalizer.CollapseWhitespace(criteria.Query);
        var userId = currentUser.UserId.Value;
        var searchedAtUtc = DateTime.UtcNow;

        if (searchHistoryScopeFactory is null)
        {
            await searchHistoryRepository.RecordSearchAsync(
                userId,
                displayQuery,
                normalizedQuery,
                searchedAtUtc,
                cancellationToken);
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = searchHistoryScopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<ISearchHistoryRepository>();
                    await repository.RecordSearchAsync(
                        userId,
                        displayQuery,
                        normalizedQuery,
                        searchedAtUtc,
                        CancellationToken.None);
                }
                catch (Exception exception)
                {
                    SearchServiceLogMessages.LogSearchHistoryFailed(logger, userId, exception);
                }
            },
            CancellationToken.None);
    }
}
