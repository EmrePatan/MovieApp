using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.TvShows;

public sealed class SearchTvShowsService(
    ITvShowDataProvider tvShowDataProvider,
    ITvShowRepository tvShowRepository,
    ICacheService cacheService) : ISearchTvShowsService
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

        var providerSearchResult = await tvShowDataProvider.SearchTvShowsAsync(
            request.Query,
            request.Page,
            request.PageSize,
            cancellationToken);

        var persistedResults = new List<TvShowSearchResult>();

        foreach (var summary in providerSearchResult.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var details = await tvShowDataProvider.GetTvShowAsync(summary.ExternalId, cancellationToken);
            if (details is null)
            {
                continue;
            }

            var tvShow = await tvShowRepository.UpsertFromProviderAsync(details, cancellationToken);
            persistedResults.Add(TvShowMapper.ToSearchResult(tvShow));
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
