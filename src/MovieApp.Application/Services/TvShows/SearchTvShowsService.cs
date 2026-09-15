using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class SearchTvShowsService(
    ITvShowDataProvider tvShowDataProvider,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ICacheService cacheService,
    IOptions<SearchOptions> searchOptions) : ISearchTvShowsService
{
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<PaginatedResult<TvShowSearchResult>> SearchAsync(
        TvShowSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var queryValidation = SearchQueryValidator.Validate(request.Query);
        if (!queryValidation.IsValid)
        {
            throw new ValidationException(queryValidation.ErrorMessage!);
        }

        var paginationValidation = SearchPaginationValidator.Validate(request.Page, request.PageSize);
        if (!paginationValidation.IsValid)
        {
            throw new ValidationException(paginationValidation.ErrorMessage!);
        }

        var cacheKey = TvShowSearchCacheKeys.Create(request.Query, request.Page, request.PageSize);
        var cachedEntry = await cacheService.GetAsync<TvShowSearchCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var options = searchOptions.Value;
        var providerSearchResult = await tvShowDataProvider.SearchTvShowsAsync(
            request.Query,
            request.Page,
            request.PageSize,
            cancellationToken);

        var detailFetchLimit = ProviderDetailIngestionHelper.ResolveDetailFetchLimit(
            request.PageSize,
            options.MaxProviderDetailFetchesPerContentType);

        var slotCount = Math.Min(detailFetchLimit, providerSearchResult.Results.Count);
        var detailSlots = new TvShowProviderDetails?[slotCount];

        await ProviderDetailIngestionHelper.IngestSummariesWithBoundedConcurrencyAsync(
            providerSearchResult.Results,
            detailFetchLimit,
            options.MaxConcurrentProviderHttpRequests,
            async (summary, index, ingestCancellationToken) =>
            {
                detailSlots[index] = await tvShowDataProvider.GetTvShowAsync(summary.ExternalId, ingestCancellationToken);
            },
            cancellationToken);

        var detailsToPersist = detailSlots
            .Take(slotCount)
            .Where(detail => detail is not null)
            .Select(detail => detail!)
            .ToList();

        var persistedResults = await SearchDetailPersistenceHelper.PersistTvShowSearchResultsAsync(
            detailsToPersist,
            catalogProviderUpsertService,
            cancellationToken);

        if (persistedResults.Count > 0)
        {
            await catalogSyncStateService.MarkRefreshedBatchAsync(
                persistedResults.Select(result => result.Id).ToList(),
                TvShowCatalogRefreshReason.DetailHydration,
                DateTime.UtcNow,
                cancellationToken);
        }

        var paginatedResult = new PaginatedResult<TvShowSearchResult>(
            persistedResults,
            providerSearchResult.Page,
            providerSearchResult.PageSize,
            providerSearchResult.TotalCount,
            providerSearchResult.TotalPages);

        await cacheService.SetAsync(
            cacheKey,
            new TvShowSearchCacheEntry { Result = paginatedResult },
            SearchCacheTtl,
            cancellationToken);

        return paginatedResult;
    }
}

