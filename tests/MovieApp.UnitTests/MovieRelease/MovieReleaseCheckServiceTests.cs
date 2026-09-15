using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.RegionalRelease;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieRelease;
using MovieApp.Application.Services.RegionalRelease;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;
using MovieApp.UnitTests.Keywords;

namespace MovieApp.UnitTests.MovieRelease;

public sealed class MovieReleaseCheckServiceTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task RunAsync_SkipsProviderFailureWithoutCreatingReleaseEvent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(-1), tmdbId: 42, updatedAt: DateTime.UtcNow);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(today));
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
        Assert.Equal(CatalogReleaseEventType.MovieReleased, Assert.Single(repository.InsertedEvents).EventType);
        Assert.Equal(1, provider.GetMovieCallCount);
        Assert.Equal(1, provider.GetReleaseDatesCallCount);
    }

    [Fact]
    public async Task RunAsync_DoesNotCreateEventWhenProviderMovesDateForward()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(today.AddDays(100)));
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Empty(repository.InsertedEvents);
        Assert.Equal(today.AddDays(100), movie.ReleaseDate);
        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task RunAsync_DoesNotCreateEventWhenProviderReturnsNullReleaseDate()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var repository = new FakeCatalogReleaseEventRepository();
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(today));
        var service = CreateService([MovieId], movie, provider, repository);

        var firstResult = await service.RunAsync();
        var secondResult = await service.RunAsync();

        Assert.Equal(1, firstResult.ReleaseEventsCreated);
        Assert.Equal(0, secondResult.ReleaseEventsCreated);
        Assert.Single(repository.InsertedEvents);
        Assert.Equal(1, provider.GetMovieCallCount);
        Assert.Equal(1, provider.GetReleaseDatesCallCount);
    }

    [Fact]
    public async Task RunAsync_CallsProviderOncePerUniqueCandidateMovieNotPerFollower()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(today));
        var service = CreateService([MovieId], movie, provider);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
        Assert.Equal(1, provider.GetMovieCallCount);
        Assert.Equal(1, provider.GetReleaseDatesCallCount);
    }

    [Fact]
    public async Task RunAsync_FarFutureMovieRefreshesRegionalMetadataOnce()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var movie = CreateMovie(futureDate, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(futureDate));
        var service = CreateService([MovieId], movie, provider);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Equal(0, provider.GetMovieCallCount);
        Assert.Equal(1, provider.GetReleaseDatesCallCount);
    }

    [Fact]
    public async Task RunAsync_StaleFarFutureMovieRefreshesMovieAndRegionalOnce()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var movie = CreateMovie(futureDate, tmdbId: 42, updatedAt: DateTime.UtcNow.AddDays(-2));
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(futureDate));
        var service = CreateService([MovieId], movie, provider);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Equal(1, provider.GetMovieCallCount);
        Assert.Equal(1, provider.GetReleaseDatesCallCount);
    }

    [Fact]
    public async Task RunAsync_RegionalFutureDoesNotCreateEvent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(-10), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var regional = CreateRegionalRelease(today.AddDays(10), syncedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(movie.ReleaseDate));
        var service = CreateService([MovieId], movie, provider, regionalRelease: regional);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
    }

    [Fact]
    public async Task RunAsync_RegionalTodayCreatesEvent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(30), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(
            CreateProviderDetails(movie.ReleaseDate),
            [Entry("TR", today, TmdbReleaseType.Theatrical, "13+")]);
        var repository = new FakeCatalogReleaseEventRepository();
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
        Assert.Equal(today, DateOnly.FromDateTime(Assert.Single(repository.InsertedEvents).ReleaseAtUtc));
    }

    [Fact]
    public async Task RunAsync_DigitalOnlyTodayCreatesEvent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today.AddDays(30), tmdbId: 42, updatedAt: DateTime.UtcNow);
        var provider = new TrackingMovieDataProvider(
            CreateProviderDetails(movie.ReleaseDate),
            [Entry("TR", today, TmdbReleaseType.Digital, null)]);
        var repository = new FakeCatalogReleaseEventRepository();
        var service = CreateService([MovieId], movie, provider, repository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
    }

    [Fact]
    public async Task RunAsync_ForwardMovedRegionalDateDoesNotCreateEarlyEvent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var staleRegional = CreateRegionalRelease(today, syncedAt: DateTime.UtcNow.AddDays(-2));
        var provider = new TrackingMovieDataProvider(
            CreateProviderDetails(today),
            [Entry("TR", today.AddDays(30), TmdbReleaseType.Theatrical, null)]);
        var repository = new FakeCatalogReleaseEventRepository();
        var service = CreateService(
            [MovieId],
            movie,
            provider,
            repository,
            regionalRelease: staleRegional);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Empty(repository.InsertedEvents);
    }

    [Fact]
    public async Task RunAsync_ProviderFailureDuringVerificationPreservesRegionalRow()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var regional = CreateRegionalRelease(today, syncedAt: DateTime.UtcNow.AddDays(-2));
        var regionalRepository = new FakeMovieRegionalReleaseRepository(regional);
        var provider = new TrackingMovieDataProvider(
            CreateProviderDetails(today),
            throwOnReleaseDates: true);
        var repository = new FakeCatalogReleaseEventRepository();
        var service = CreateService(
            [MovieId],
            movie,
            provider,
            repository,
            regionalRepository);

        var result = await service.RunAsync();

        Assert.Equal(0, result.ReleaseEventsCreated);
        Assert.Equal(1, result.SkippedProviderFailures);
        Assert.Equal(today, regional.EffectiveReleaseDate);
    }

    [Fact]
    public async Task RunAsync_SuccessfulNoTrPersistsGlobalFallback()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var movie = CreateMovie(today, tmdbId: 42, updatedAt: DateTime.UtcNow);
        var regionalRepository = new FakeMovieRegionalReleaseRepository(null);
        var provider = new TrackingMovieDataProvider(CreateProviderDetails(today), []);
        var repository = new FakeCatalogReleaseEventRepository();
        var service = CreateService(
            [MovieId],
            movie,
            provider,
            repository,
            regionalRepository);

        var result = await service.RunAsync();

        Assert.Equal(1, result.ReleaseEventsCreated);
        Assert.NotNull(regionalRepository.StoredRegionalRelease);
        Assert.True(regionalRepository.StoredRegionalRelease!.IsFallbackGlobal);
        Assert.Equal(today, regionalRepository.StoredRegionalRelease.EffectiveReleaseDate);
    }

    private static MovieReleaseCheckService CreateService(
        IReadOnlyList<Guid> followedMovieIds,
        Movie movie,
        TrackingMovieDataProvider provider,
        FakeCatalogReleaseEventRepository? releaseRepository = null,
        FakeMovieRegionalReleaseRepository? regionalRepository = null,
        MovieRegionalRelease? regionalRelease = null)
    {
        releaseRepository ??= new FakeCatalogReleaseEventRepository();
        regionalRepository ??= new FakeMovieRegionalReleaseRepository(regionalRelease);

        var movieRepository = new FakeMovieRepository(movie);
        return new MovieReleaseCheckService(
            new FakeCatalogFollowRepository(followedMovieIds),
            movieRepository,
            regionalRepository,
            provider,
            provider,
            CatalogProviderUpsertTestDoubles.CreateRepositoryBackedUpsertService(movieRepository),
            releaseRepository,
            new RegionalEffectiveReleaseResolver(),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }));
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

    private static MovieRegionalRelease CreateRegionalRelease(DateOnly effectiveDate, DateTime syncedAt) =>
        new()
        {
            MovieId = MovieId,
            Region = "TR",
            EffectiveReleaseDate = effectiveDate,
            EffectiveReleaseType = TmdbReleaseType.Theatrical,
            SyncedAtUtc = syncedAt
        };

    private static RegionalMovieReleaseEntry Entry(
        string region,
        DateOnly releaseDate,
        TmdbReleaseType type,
        string? certification) =>
        new(region, releaseDate, type, certification, 0);

    private sealed class FakeCatalogFollowRepository(IReadOnlyList<Guid> movieIds) : ICatalogFollowRepository
    {
        public Task<IReadOnlyList<Guid>> GetFollowedMovieIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(movieIds);

        public Task<CatalogFollow?> GetForUserAndContentAsync(
            Guid userId,
            CatalogContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogFollow?> GetForUserAndContentForUpdateAsync(
            Guid userId,
            CatalogContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RemoveAsync(
            Guid userId,
            CatalogContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
            Guid userId,
            int page,
            int pageSize,
            CatalogContentType? contentType = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetEstablishedTvFollowsByTvShowIdsAsync(
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsForReleaseCheckAsync(
            CancellationToken cancellationToken = default) =>
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

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default)
        {
            movie.ReleaseDate = details.ReleaseDate;
            movie.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(movie);
        }
    }

    private sealed class FakeMovieRegionalReleaseRepository : IMovieRegionalReleaseRepository
    {
        public FakeMovieRegionalReleaseRepository(MovieRegionalRelease? initialRegionalRelease)
        {
            StoredRegionalRelease = initialRegionalRelease;
        }

        public MovieRegionalRelease? StoredRegionalRelease { get; private set; }

        public Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
            Guid movieId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(StoredRegionalRelease);

        public Task<MovieRegionalRelease> UpsertAsync(
            MovieRegionalRelease regionalRelease,
            CancellationToken cancellationToken = default)
        {
            StoredRegionalRelease = regionalRelease;
            return Task.FromResult(regionalRelease);
        }
    }

    private sealed class TrackingMovieDataProvider : IMovieDataProvider, IMovieReleaseDatesProvider
    {
        private readonly MovieProviderDetails? _details;
        private readonly IReadOnlyList<RegionalMovieReleaseEntry> _releaseEntries;
        private readonly bool _throwOnReleaseDates;

        private int _getMovieCallCount;
        private int _getReleaseDatesCallCount;

        public TrackingMovieDataProvider(
            MovieProviderDetails? details,
            IReadOnlyList<RegionalMovieReleaseEntry>? releaseEntries = null,
            bool throwOnReleaseDates = false)
        {
            _details = details;
            _releaseEntries = releaseEntries ?? [];
            _throwOnReleaseDates = throwOnReleaseDates;
        }

        public int GetMovieCallCount => _getMovieCallCount;

        public int GetReleaseDatesCallCount => _getReleaseDatesCallCount;

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, CancellationToken cancellationToken = default)
        {
            _getMovieCallCount++;
            return Task.FromResult(_details);
        }

        public Task<IReadOnlyList<RegionalMovieReleaseEntry>> GetMovieReleaseDatesAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            _getReleaseDatesCallCount++;
            if (_throwOnReleaseDates)
            {
                throw new InvalidOperationException("Provider failure.");
            }

            return Task.FromResult(_releaseEntries);
        }
    }

    private sealed class FakeCatalogReleaseEventRepository : ICatalogReleaseEventRepository
    {
        private readonly HashSet<string> _dedupeKeys = [];

        public List<CatalogReleaseEvent> InsertedEvents { get; } = [];

        public Task<bool> ExistsByDedupeKeyAsync(string dedupeKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_dedupeKeys.Contains(dedupeKey));

        public Task<HashSet<string>> GetDedupeKeysForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CatalogReleaseEventInsertResult> TryAddEventsAsync(
            IReadOnlyList<CatalogReleaseEvent> events,
            CancellationToken cancellationToken = default)
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
