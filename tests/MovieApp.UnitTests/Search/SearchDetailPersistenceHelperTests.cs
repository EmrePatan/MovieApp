using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Search;

public sealed class SearchDetailPersistenceHelperTests
{
    [Fact]
    public async Task PersistMovieSearchResultsAsyncPropagatesUnrelatedRepositoryExceptions()
    {
        var details = new[] { CreateMovieDetails(910001, "tt9100001", "Movie One") };
        var summaries = new[] { CreateSummary(910001, "fake-tmdb-910001", "Movie One") };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SearchDetailPersistenceHelper.PersistMovieSearchResultsAsync(
                details,
                summaries,
                new ThrowingMovieRepository(new InvalidOperationException("database unavailable")),
                NullLogger.Instance,
                CancellationToken.None));
    }

    [Fact]
    public async Task PersistMovieSearchResultsAsyncHonorsCancellationDuringFallback()
    {
        var details = new[]
        {
            CreateMovieDetails(910001, "tt9100001", "Movie One"),
            CreateMovieDetails(910002, "tt9100002", "Movie Two")
        };
        var summaries = details
            .Select(detail => CreateSummary(detail.TmdbId!.Value, detail.ExternalId, detail.Title))
            .ToList();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var repository = new ConflictThenThrowOnSecondSingleUpsertRepository();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SearchDetailPersistenceHelper.PersistMovieSearchResultsAsync(
                details,
                summaries,
                repository,
                NullLogger.Instance,
                cts.Token));
    }

    private static MovieProviderDetails CreateMovieDetails(int tmdbId, string imdbId, string title) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: imdbId,
            Title: title,
            OriginalTitle: title,
            Overview: "Overview",
            ReleaseDate: new DateOnly(2020, 1, 1),
            RuntimeMinutes: 120,
            PosterPath: "/poster.jpg",
            BackdropPath: "/backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 8.0m,
            VoteCount: 100,
            Genres: ["Drama"]);

    private static MovieProviderSummary CreateSummary(int tmdbId, string externalId, string title) =>
        new(
            externalId,
            tmdbId,
            null,
            $"tt{tmdbId:D7}",
            title,
            "Overview",
            new DateOnly(2020, 1, 1),
            "/poster.jpg",
            8.0m,
            100);

    private sealed class ThrowingMovieRepository(Exception exception) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw exception;

        public Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class ConflictThenThrowOnSecondSingleUpsertRepository : IMovieRepository
    {
        private int _singleUpsertCount;

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw new MovieExternalIdPersistenceConflictException(null, string.Empty);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default)
        {
            _singleUpsertCount++;
            cancellationToken.ThrowIfCancellationRequested();

            if (_singleUpsertCount == 1)
            {
                return Task.FromResult(new Movie
                {
                    Id = Guid.NewGuid(),
                    TmdbId = details.TmdbId,
                    Title = details.Title
                });
            }

            throw new OperationCanceledException();
        }
    }
}
