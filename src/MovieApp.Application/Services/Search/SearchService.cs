using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Common;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Search;

public sealed class SearchService(
    ISearchRepository searchRepository,
    ISearchHistoryRepository searchHistoryRepository,
    ICurrentUser currentUser,
    ICacheService cacheService) : ISearchService
{
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(5);

    public async Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var validation = AdvancedSearchValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var cacheKey = UnifiedSearchCacheKeys.Create(criteria);
        var cachedEntry = await cacheService.GetAsync<UnifiedSearchCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            await TryRecordSearchHistoryAsync(criteria, cancellationToken);
            return cachedEntry.Result;
        }

        var result = await searchRepository.SearchAsync(criteria, cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new UnifiedSearchCacheEntry { Result = result },
            SearchCacheTtl,
            cancellationToken);

        await TryRecordSearchHistoryAsync(criteria, cancellationToken);

        return result;
    }

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

        await searchHistoryRepository.RecordSearchAsync(
            currentUser.UserId.Value,
            displayQuery,
            normalizedQuery,
            DateTime.UtcNow,
            cancellationToken);
    }
}
