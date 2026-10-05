using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Application.Services.Search;
using static MovieApp.Application.Services.Search.PopularDiscoverQuality;
using MovieApp.Application.Services.TvShowChanges;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.UnitTests.Persistence;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Catalog;

public sealed class CatalogMetadataFreshnessTests
{
    [Fact]
    public void RefreshMaps_Union_DeduplicatesWhenBothRelevant()
    {
        var movieId = Guid.NewGuid();
        var user = new Dictionary<int, Guid> { [1] = movieId };
        var discovery = new Dictionary<int, Guid> { [1] = movieId };

        var maps = CatalogChangesRefreshMaps.Create(user, discovery);

        Assert.Single(maps.UnionByTmdbId);
        Assert.Equal(movieId, maps.UnionByTmdbId[1]);
    }

    [Fact]
    public void RefreshMaps_DiscoveryOnly_AddsToUnion()
    {
        var discoveryId = Guid.NewGuid();
        var maps = CatalogChangesRefreshMaps.Create(
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid> { [42] = discoveryId });

        Assert.Empty(maps.UserRelevantByTmdbId);
        Assert.Equal(discoveryId, maps.UnionByTmdbId[42]);
    }

    [Fact]
    public async Task ApplyProviderDetails_SetsMetadataFreshnessTimestamp()
    {
        await using var context = CreateContext();
        var repository = CreateMovieRepository(context);
        var details = new MovieProviderDetails(
            "1",
            1,
            null,
            null,
            "Title",
            null,
            "Overview",
            new DateOnly(2024, 1, 1),
            100,
            null,
            null,
            "en",
            8m,
            1000,
            []);

        var movie = await repository.UpsertFromProviderAsync(details);

        Assert.NotNull(movie.TmdbMetadataUpdatedAtUtc);
    }

    [Fact]
    public async Task FailedRefresh_DoesNotAdvanceFreshnessTimestamp()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = 99,
            Title = "Stale",
            VoteCount = MinimumVoteCountMovie,
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30),
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10),
            TmdbMetadataUpdatedAtUtc = DateTime.UtcNow.AddDays(-10)
        });
        await context.SaveChangesAsync();

        var refresh = new FailingMovieRefreshService();
        var safetyNet = new CatalogMetadataFreshnessSafetyNetService(
            CreateFreshnessRepository(context),
            refresh,
            new NoOpTvRefreshService(),
            Options.Create(new CatalogMetadataFreshnessOptions
            {
                SafetyNetEnabled = true,
                SafetyNetBatchSize = 10,
                SafetyNetMaxItemsPerRun = 10,
                FreshnessThresholdHours = 1
            }),
            NullLogger<CatalogMetadataFreshnessSafetyNetService>.Instance);

        var before = await context.Movies.AsNoTracking().SingleAsync(movie => movie.Id == movieId);
        await safetyNet.RunAsync();
        var after = await context.Movies.AsNoTracking().SingleAsync(movie => movie.Id == movieId);

        Assert.Equal(before.TmdbMetadataUpdatedAtUtc, after.TmdbMetadataUpdatedAtUtc);
        Assert.Equal(1, refresh.Attempts);
    }

    [Fact]
    public async Task DiscoveryRelevant_RecentRelease_QualifiesWithoutVoteFloor()
    {
        await using var context = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        context.Movies.Add(new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = 5001,
            Title = "New",
            VoteCount = 5,
            ReleaseDate = today.AddDays(-7),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var relevance = CreateRelevanceRepository(context);
        var discovery = await relevance.GetDiscoveryRelevantMovieIdsByTmdbIdAsync();

        Assert.Contains(5001, discovery.Keys);
    }

    [Fact]
    public async Task StaleDiscoveryRelevant_IsSafetyNetCandidate()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = 6001,
            Title = "Stale discovery",
            VoteCount = MinimumVoteCountMovie,
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TmdbMetadataUpdatedAtUtc = DateTime.UtcNow.AddDays(-10)
        });
        await context.SaveChangesAsync();

        var freshness = CreateFreshnessRepository(context);
        var staleBefore = DateTime.UtcNow.AddHours(-1);
        var ids = await freshness.SelectStaleDiscoveryRelevantMovieIdsAsync(staleBefore, 10);

        Assert.Contains(movieId, ids);
    }

    [Fact]
    public async Task FreshDiscoveryRelevant_IsNotSafetyNetCandidate()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = 6002,
            Title = "Fresh discovery",
            VoteCount = MinimumVoteCountMovie,
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TmdbMetadataUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });
        await context.SaveChangesAsync();

        var freshness = CreateFreshnessRepository(context);
        var ids = await freshness.SelectStaleDiscoveryRelevantMovieIdsAsync(
            DateTime.UtcNow.AddHours(-72),
            10);

        Assert.DoesNotContain(movieId, ids);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static MovieRepository CreateMovieRepository(ApplicationDbContext context) =>
        new(
            context,
            new NoOpContentSearchTitleSynchronizer(),
            new NoOpMovieCatalogDetailsCacheInvalidator());

    private static CatalogChangesRelevanceRepository CreateRelevanceRepository(ApplicationDbContext context) =>
        new(
            context,
            Options.Create(new NewReleasesOptions()),
            Options.Create(new RecommendationOptions()),
            Options.Create(new TopRatedOptions()),
            Options.Create(new CatalogMetadataFreshnessOptions()));

    private static CatalogMetadataFreshnessRepository CreateFreshnessRepository(ApplicationDbContext context) =>
        new(
            context,
            Options.Create(new NewReleasesOptions()),
            Options.Create(new RecommendationOptions()),
            Options.Create(new TopRatedOptions()),
            Options.Create(new CatalogMetadataFreshnessOptions()));

    [Fact]
    public async Task SafetyNet_RespectsMaxItemsPerRun()
    {
        await using var context = CreateContext();
        for (var i = 0; i < 5; i++)
        {
            context.Movies.Add(new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = 7000 + i,
                Title = $"Stale {i}",
                VoteCount = MinimumVoteCountMovie,
                ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TmdbMetadataUpdatedAtUtc = DateTime.UtcNow.AddDays(-10)
            });
        }

        await context.SaveChangesAsync();

        var recording = new RecordingMovieRefreshService();
        var safetyNet = new CatalogMetadataFreshnessSafetyNetService(
            CreateFreshnessRepository(context),
            recording,
            new NoOpTvRefreshService(),
            Options.Create(new CatalogMetadataFreshnessOptions
            {
                SafetyNetEnabled = true,
                SafetyNetBatchSize = 10,
                SafetyNetMaxItemsPerRun = 4,
                FreshnessThresholdHours = 1
            }),
            NullLogger<CatalogMetadataFreshnessSafetyNetService>.Instance);

        var result = await safetyNet.RunAsync();

        Assert.Equal(2, result.SelectedBatchCount);
        Assert.True(recording.RefreshedMovieIds.Count <= 4);
        Assert.Equal(result.SelectedBatchCount, recording.RefreshedMovieIds.Count);
    }

    private sealed class RecordingMovieRefreshService : IMovieChangesTargetedRefreshService
    {
        public List<Guid> RefreshedMovieIds { get; } = [];

        public Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            RefreshedMovieIds.Add(movieId);
            return Task.FromResult(TmdbChangesTargetRefreshResult.Refreshed([]));
        }
    }

    private sealed class FailingMovieRefreshService : IMovieChangesTargetedRefreshService
    {
        public int Attempts { get; private set; }

        public Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(TmdbChangesTargetRefreshResult.Failed());
        }
    }

    private sealed class NoOpTvRefreshService : ITvShowChangesTargetedRefreshService
    {
        public Task<TmdbChangesTargetRefreshResult> RefreshRelevantShowAsync(
            Guid tvShowId,
            DateOnly boundaryDate,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TmdbChangesTargetRefreshResult.SkippedNotFound());
    }
}
