using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.CatalogFollows;

public sealed class GetCatalogUpcomingService(
    ICurrentUser currentUser,
    ICatalogFollowCatalogRepository catalogFollowCatalogRepository,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
    ILogger<GetCatalogUpcomingService> logger) : IGetCatalogUpcomingService
{
    public async Task<CatalogUpcomingListResult> GetAsync(
        int page,
        int pageSize,
        CatalogUpcomingScope scope = CatalogUpcomingScope.Catalog,
        string? releaseRegion = null,
        string contentLocale = ContentLocaleResolver.EnglishUnitedStates,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var region = ResolveRegion(releaseRegion);

        var repositoryStopwatch = Stopwatch.StartNew();
        (IReadOnlyList<CatalogUpcomingItemResult> items, int totalCount) result = scope switch
        {
            CatalogUpcomingScope.Followed => await GetFollowedAsync(page, pageSize, today, region, cancellationToken),
            _ => await GetCatalogAsync(page, pageSize, today, region, cancellationToken)
        };
        repositoryStopwatch.Stop();

        var (items, totalCount) = result;
        var localizedItems = await summaryLocalizationOverlayService.ApplyToUpcomingItemsAsync(
            items,
            contentLocale,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        totalStopwatch.Stop();
        CatalogUpcomingPerfLogMessages.LogRequest(
            logger,
            scope,
            totalStopwatch.ElapsedMilliseconds,
            repositoryStopwatch.ElapsedMilliseconds,
            page,
            pageSize,
            totalCount,
            localizedItems.Count,
            currentUser.IsAuthenticated);

        return new CatalogUpcomingListResult(localizedItems, page, pageSize, totalCount, totalPages);
    }

    private async Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetCatalogAsync(
        int page,
        int pageSize,
        DateOnly today,
        string region,
        CancellationToken cancellationToken)
    {
        Guid? userId = null;
        if (currentUser.IsAuthenticated)
        {
            userId = CurrentUserGuard.RequireUserId(currentUser);
        }

        return await catalogFollowCatalogRepository.GetUpcomingCatalogAsync(
            userId,
            page,
            pageSize,
            today,
            region,
            cancellationToken);
    }

    private async Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetFollowedAsync(
        int page,
        int pageSize,
        DateOnly today,
        string region,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new AuthenticationException("Authentication is required for followed upcoming catalog.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await catalogFollowCatalogRepository.GetFollowedUpcomingCatalogAsync(
            userId,
            page,
            pageSize,
            today,
            region,
            cancellationToken);
    }

    private string ResolveRegion(string? releaseRegion)
    {
        if (!string.IsNullOrWhiteSpace(releaseRegion))
        {
            return WatchProviderRegionValidator.Normalize(releaseRegion);
        }

        return WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);
    }
}
