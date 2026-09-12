using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Movies;

public sealed class SearchMoviesService(
    IMovieDataProvider movieDataProvider,
    IMovieRepository movieRepository,
    ICacheService cacheService,
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

        var providerSearchResult = await movieDataProvider.SearchMoviesAsync(
            request.Query,
            request.Page,
            request.PageSize,
            cancellationToken);

        var persistedResults = new List<MovieSearchResult>();

        foreach (var summary in providerSearchResult.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var details = await movieDataProvider.GetMovieAsync(summary.ExternalId, cancellationToken);
            if (details is null)
            {
                continue;
            }

            Movie movie;

            try
            {
                movie = await movieRepository.UpsertFromProviderAsync(details, cancellationToken);
            }
            catch (MovieExternalIdPersistenceConflictException)
            {
                SearchMoviesLogMessages.LogSkippedSearchResultPersistenceConflict(
                    logger,
                    summary.ExternalId,
                    summary.TmdbId);
                continue;
            }

            persistedResults.Add(MovieMapper.ToSearchResult(movie));
        }

        var paginatedResult = new PaginatedResult<MovieSearchResult>(
            persistedResults,
            providerSearchResult.Page,
            providerSearchResult.PageSize,
            providerSearchResult.TotalCount,
            providerSearchResult.TotalPages);

        var cacheEntry = new MovieSearchCacheEntry
        {
            Result = paginatedResult
        };

        await cacheService.SetAsync(cacheKey, cacheEntry, SearchCacheTtl, cancellationToken);

        return paginatedResult;
    }
}

