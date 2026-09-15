using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieRelease;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Keywords;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.MovieRelease;

public sealed class MovieReleaseCheckServiceTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task RunAsync_SkipsProviderFailureWithoutCreatingReleaseEvent()
    {
        var movie = CreateMovie(new DateOnly(2026, 9, 10), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(null);
        var service = CreateService([MovieId], movie, provider);

        var result = await service.RunAsync();

        Assert.Equal(1, result.MoviesChecked);
        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Equal(1, result.SkippedProviderFailures);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_CreatesMovieReleasedEventWhenProviderConfirmsReachedDate()
    {
        var movie = CreateMovie(new DateOnly(2026, 9, 14), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(new DateOnly(2026, 9, 14)));
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
        Assert.Equal(CatalogReleaseEventType.MovieReleased, Assert.Single(repository.InsertedEvents).EventType);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_DoesNotCreateEventWhenProviderMovesDateForward()
    {
        var movie = CreateMovie(new DateOnly(2026, 9, 14), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(new DateOnly(2026, 12, 25)));
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Empty(repository.InsertedEvents);
        Assert.Equal(new DateOnly(2026, 12, 25), movie.ReleaseDate);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_DoesNotCreateEventWhenProviderReturnsNullReleaseDate()
    {
        var movie = CreateMovie(new DateOnly(2026, 9, 14), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(null));
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Empty(repository.InsertedEvents);
        Assert.Null(movie.ReleaseDate);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_DuplicateExecutionDoesNotCreateDuplicateEvent()
    {
        var movie = CreateMovie(new DateOnly(2026, 9, 14), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(new DateOnly(2026, 9, 14)));
        var service = CreateService([MovieId], movie, provider, repository);

        var firstResult = await service.RunAsync();
        var secondResult = await service.RunAsync();

        Assert.Equal(1, firstResult.ReleaseEventsCreated);
        Assert.Equal(0, secondResult.ReleaseEventsCreated);
        Assert.Single(repository.InsertedEvents);
        Assert.Equal(2, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_CallsProviderOncePerUniqueCandidateMovieNotPerFollower()
    {
        var movie = CreateMovie(new DateOnly(2026, 9, 14), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(new DateOnly(2026, 9, 14)));
        var service = CreateService(
            [MovieId],
            movie,
            provider);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_FarFutureMovieUsesTtlRefreshWithoutCandidateVerification()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var movie = CreateMovie(futureDate, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(futureDate));
        var service = CreateService([MovieId], movie, provider);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Equal(0, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_StaleFarFutureMovieRefreshesFromProviderOnce()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var movie = CreateMovie(futureDate, tmdbId: 42, updatedAt: DateTime.UtcNow.AddDays(-2));
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(futureDate));
        var service = CreateService([MovieId], movie, provider);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    private static MovieReleaseCheckService CreateService(
        IReadOnlyList<Guid> followedMovieIds,
        Movie movie,
        TrackingMovieDataProvider provider,
        FakeCatalogReleaseEventRepository? releaseRepository = null)
    {
        releaseRepository ??= new FakeCatalogReleaseEventRepository();

        var movieRepository = new FakeMovieRepository(movie);
        return new MovieReleaseCheckService(
            new FakeCatalogFollowRepository(followedMovieIds),
            movieRepository,
            provider,
            CatalogProviderUpsertTestDoubles.CreateRepositoryBackedUpsertService(movieRepository),
            releaseRepository);
    }

    private static MovieProviderDetails CreateProviderDetails(DateOnly? releaseDate) =>
        new(
            "42",
            42,
            null,
            null,
            "Future Movie",
            null,
            null,
            releaseDate,
            null,
            null,
            null,
            null,
            0,
            0,
            []);

    private static Movie CreateMovie(DateOnly releaseDate, int? tmdbId, DateTime updatedAt) =>
        new()
        {
            Id = MovieId,
            TmdbId = tmdbId,
            Title = "Movie",
            ReleaseDate = releaseDate,
            UpdatedAt = updatedAt,
            CreatedAt = updatedAt
        };

    private sealed class FakeCatalogFollowRepository(IReadOnlyList<Guid> movieIds) : ICatalogFollowRepository
    {
        public Task<IReadOnlyList<Guid>> GetFollowedMovieIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(movieIds);

        public Task<CatalogFollow?> GetForUserAndContentAsync(Guid userId, CatalogContentType contentType, Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogFollow?> GetForUserAndContentForUpdateAsync(Guid userId, CatalogContentType contentType, Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RemoveAsync(Guid userId, CatalogContentType contentType, Guid contentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(Guid userId, int page, int pageSize, CatalogContentType? contentType = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetEstablishedTvFollowsByTvShowIdsAsync(IReadOnlyCollection<Guid> tvShowIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsForReleaseCheckAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveMovieFollowsByMovieIdAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeMovieRepository(Movie movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(movie);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default)
        {
            movie.ReleaseDate = details.ReleaseDate;
            movie.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(movie);
        }
    }

    private sealed class TrackingMovieDataProvider(MovieProviderDetails? details) : IMovieDataProvider
    {
        private int _getMovieCallCount;

        public int GetMovieCallCount => _getMovieCallCount;

        public Task<MovieProviderSearchResult> SearchMoviesAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, CancellationToken cancellationToken = default)
        {
            _getMovieCallCount++;
            return Task.FromResult(details);
        }
    }

    private sealed class FakeCatalogReleaseEventRepository : ICatalogReleaseEventRepository
    {
        private readonly HashSet<string> _dedupeKeys = [];

        public List<CatalogReleaseEvent> InsertedEvents { get; } = [];

        public Task<HashSet<string>> GetDedupeKeysForTvShowAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogReleaseEventInsertResult> TryAddEventsAsync(IReadOnlyList<CatalogReleaseEvent> events, CancellationToken cancellationToken = default)
        {
            var created = 0;
            var createdIds = new List<Guid>();

            foreach (var releaseEvent in events)
            {
                if (!_dedupeKeys.Add(releaseEvent.DedupeKey))
                {
                    continue;
                }

                InsertedEvents.Add(releaseEvent);
                created++;
                createdIds.Add(releaseEvent.Id);
            }

            return Task.FromResult(new CatalogReleaseEventInsertResult(created, events.Count - created, createdIds));
        }
    }
}
