using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Movies;

namespace MovieApp.Application.Services.Search;

internal static class SearchDetailPersistenceHelper
{
    internal static async Task<IReadOnlyList<MovieSearchResult>> PersistMovieSearchResultsAsync(
        IReadOnlyList<MovieProviderDetails> details,
        IReadOnlyList<MovieProviderSummary> summaries,
        IMovieRepository movieRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (details.Count == 0)
        {
            return [];
        }

        try
        {
            var movies = await movieRepository.UpsertFromProviderBatchAsync(details, cancellationToken);
            return movies.Select(MovieMapper.ToSearchResult).ToList();
        }
        catch (MovieExternalIdPersistenceConflictException)
        {
            return await PersistMovieSearchResultsIndividuallyAsync(
                details,
                summaries,
                movieRepository,
                logger,
                cancellationToken);
        }
    }

    internal static async Task<IReadOnlyList<TvShowSearchResult>> PersistTvShowSearchResultsAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        ITvShowRepository tvShowRepository,
        CancellationToken cancellationToken)
    {
        if (details.Count == 0)
        {
            return [];
        }

        var tvShows = await tvShowRepository.UpsertFromProviderBatchAsync(details, cancellationToken);
        return tvShows.Select(TvShowMapper.ToSearchResult).ToList();
    }

    private static async Task<IReadOnlyList<MovieSearchResult>> PersistMovieSearchResultsIndividuallyAsync(
        IReadOnlyList<MovieProviderDetails> details,
        IReadOnlyList<MovieProviderSummary> summaries,
        IMovieRepository movieRepository,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var results = new List<MovieSearchResult>(details.Count);

        for (var index = 0; index < details.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = details[index];

            try
            {
                var movie = await movieRepository.UpsertFromProviderAsync(detail, cancellationToken);
                results.Add(MovieMapper.ToSearchResult(movie));
            }
            catch (MovieExternalIdPersistenceConflictException)
            {
                if (index < summaries.Count)
                {
                    SearchMoviesLogMessages.LogSkippedSearchResultPersistenceConflict(
                        logger,
                        summaries[index].ExternalId,
                        summaries[index].TmdbId);
                }
            }
        }

        return results;
    }
}
