using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Keywords;

public sealed class CatalogProviderUpsertServiceTests
{
    [Fact]
    public async Task UpsertMovieFromProviderAsyncSkipsKeywordEnrichmentByDefault()
    {
        var movieRepository = new FakeMovieRepository();
        var keywordIngestion = new TrackingKeywordIngestionService();
        var service = new CatalogProviderUpsertService(
            movieRepository,
            new FakeTvShowRepository(),
            keywordIngestion);

        await service.UpsertMovieFromProviderAsync(CreateMovieDetails());

        Assert.Equal(0, keywordIngestion.MovieCalls);
    }

    [Fact]
    public async Task UpsertMovieFromProviderAsyncEnrichesKeywordsWhenRequested()
    {
        var movieRepository = new FakeMovieRepository();
        var keywordIngestion = new TrackingKeywordIngestionService();
        var service = new CatalogProviderUpsertService(
            movieRepository,
            new FakeTvShowRepository(),
            keywordIngestion);

        await service.UpsertMovieFromProviderAsync(CreateMovieDetails(), enrichKeywords: true);

        Assert.Equal(1, keywordIngestion.MovieCalls);
    }

    private static MovieProviderDetails CreateMovieDetails() =>
        new(
            "tmdb-1",
            1,
            null,
            null,
            "Movie",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            0,
            []);

    private sealed class FakeMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = details.TmdbId,
                Title = details.Title
            });

        public Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TrackingKeywordIngestionService : ICatalogKeywordIngestionService
    {
        public int MovieCalls { get; private set; }

        public Task TryEnrichMovieKeywordsAsync(
            Guid movieId,
            bool refreshKeywords,
            CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            return Task.CompletedTask;
        }

        public Task TryEnrichTvShowKeywordsAsync(
            Guid tvShowId,
            bool refreshKeywords,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
