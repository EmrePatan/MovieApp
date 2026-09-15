using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Movies;

public sealed class SearchMoviesService(
    IMovieDataProvider movieDataProvider,
    IMovieRepository movieRepository,
    ICacheService cacheService,
    IOptions<SearchOptions> searchOptions,
    ILogger<SearchMoviesService> logger) : ISearchMoviesService
{
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<PaginatedResult<MovieSearchResult>> SearchAsync(
        MovieSearchRequest request,
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

        var cacheKey = MovieSearchCacheKeys.Create(request.Query, request.Page, request.PageSize);
        var cachedEntry = await cacheService.GetAsync<MovieSearchCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var options = searchOptions.Value;
        var providerSearchResult = await movieDataProvider.SearchMoviesAsync(
            request.Query,
            request.Page,
            request.PageSize,
            cancellationToken);

        var detailFetchLimit = ProviderDetailIngestionHelper.ResolveDetailFetchLimit(
            request.PageSize,
            options.MaxProviderDetailFetchesPerContentType);

        var slotCount = Math.Min(detailFetchLimit, providerSearchResult.Results.Count);
        var detailSlots = new MovieProviderDetails?[slotCount];

        await ProviderDetailIngestionHelper.IngestSummariesWithBoundedConcurrencyAsync(
            providerSearchResult.Results,
            detailFetchLimit,
            options.MaxConcurrentProviderHttpRequests,
            async (summary, index, ingestCancellationToken) =>
            {
                detailSlots[index] = await movieDataProvider.GetMovieAsync(summary.ExternalId, ingestCancellationToken);
            },
            cancellationToken);

        var detailsToPersist = detailSlots
            .Take(slotCount)
            .Where(detail => detail is not null)
            .Select(detail => detail!)
            .ToList();

        var persistedResults = await SearchDetailPersistenceHelper.PersistMovieSearchResultsAsync(
            detailsToPersist,
            providerSearchResult.Results,
            movieRepository,
            logger,
            cancellationToken);

        var paginatedResult = new PaginatedResult<MovieSearchResult>(
            persistedResults,
            providerSearchResult.Page,
            providerSearchResult.PageSize,
            providerSearchResult.TotalCount,
            providerSearchResult.TotalPages);

        await cacheService.SetAsync(
            cacheKey,
            new MovieSearchCacheEntry { Result = paginatedResult },
            SearchCacheTtl,
            cancellationToken);

        return paginatedResult;
    }
}
