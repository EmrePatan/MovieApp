using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Keywords;

internal static class CatalogProviderUpsertTestDoubles
{
    internal static ICatalogProviderUpsertService CreateRepositoryBackedUpsertService(
        IMovieRepository? movieRepository = null,
        ITvShowRepository? tvShowRepository = null) =>
        new RepositoryBackedCatalogProviderUpsertService(movieRepository, tvShowRepository);

    internal static ICatalogKeywordIngestionService CreateNoOpKeywordIngestionService() =>
        new NoOpCatalogKeywordIngestionService();

    private sealed class RepositoryBackedCatalogProviderUpsertService(
        IMovieRepository? movieRepository,
        ITvShowRepository? tvShowRepository) : ICatalogProviderUpsertService
    {
        public Task<Movie> UpsertMovieFromProviderAsync(
            MovieProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            if (movieRepository is null)
            {
                throw new NotSupportedException();
            }

            return movieRepository.UpsertFromProviderAsync(details, cancellationToken);
        }

        public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            if (movieRepository is null)
            {
                throw new NotSupportedException();
            }

            return movieRepository.UpsertFromProviderBatchAsync(details, cancellationToken);
        }

        public Task<TvShow> UpsertTvShowFromProviderAsync(
            TvShowProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            if (tvShowRepository is null)
            {
                throw new NotSupportedException();
            }

            return tvShowRepository.UpsertFromProviderAsync(details, cancellationToken);
        }

        public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            if (tvShowRepository is null)
            {
                throw new NotSupportedException();
            }

            return tvShowRepository.UpsertFromProviderBatchAsync(details, cancellationToken);
        }
    }

    private sealed class NoOpCatalogKeywordIngestionService : ICatalogKeywordIngestionService
    {
        public Task TryEnrichMovieKeywordsAsync(
            Guid movieId,
            bool refreshKeywords,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryEnrichTvShowKeywordsAsync(
            Guid tvShowId,
            bool refreshKeywords,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
