using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Services.TvShowChanges;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Catalog;

[Collection("CatalogPersistence")]
public sealed class CatalogMetadataFreshnessIntegrationTests
{
    private static readonly DateTime UtcNow = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DiscoveryRelevanceRecentReleaseQualifiesWithoutVoteFloor()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(context, tmdbId: 81001, voteCount: 3, releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-7));

        var map = await CreateRelevanceRepository(context).GetDiscoveryRelevantMovieIdsByTmdbIdAsync();

        Assert.Contains(81001, map.Keys);
    }

    [Fact]
    public async Task DiscoveryRelevanceUpcomingReleaseQualifiesWithoutVoteFloor()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(context, tmdbId: 81002, voteCount: 2, releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(30));

        var map = await CreateRelevanceRepository(context).GetDiscoveryRelevantMovieIdsByTmdbIdAsync();

        Assert.Contains(81002, map.Keys);
    }

    [Fact]
    public async Task DiscoveryRelevanceRecommendationVoteFloorQualifies()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(
            context,
            tmdbId: 81003,
            voteCount: 25,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddYears(-5));

        var map = await CreateRelevanceRepository(context).GetDiscoveryRelevantMovieIdsByTmdbIdAsync();

        Assert.Contains(81003, map.Keys);
    }

    [Fact]
    public async Task DiscoveryRelevanceProviderDiscoverySeenQualifies()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(
            context,
            tmdbId: 81004,
            voteCount: 1,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddYears(-5),
            providerSeenAtUtc: UtcNow.AddDays(-2));

        var map = await CreateRelevanceRepository(context).GetDiscoveryRelevantMovieIdsByTmdbIdAsync();

        Assert.Contains(81004, map.Keys);
    }

    [Fact]
    public async Task DiscoveryRelevanceIrrelevantRowExcluded()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(
            context,
            tmdbId: 81005,
            voteCount: 5,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddYears(-10));

        var map = await CreateRelevanceRepository(context).GetDiscoveryRelevantMovieIdsByTmdbIdAsync();

        Assert.DoesNotContain(81005, map.Keys);
    }

    [Fact]
    public async Task ChangesRefreshMapsUserRelevantChangedIdIsSelected()
    {
        await using var context = await CreateIsolatedContextAsync();
        var userId = Guid.NewGuid();
        var movieId = Guid.NewGuid();
        context.Users.Add(User.Create(userId, $"u-{userId:N}@test.com", "hash", "Test", UtcNow));
        await SeedMovieAsync(context, tmdbId: 82001, movieId: movieId, voteCount: 5, releaseDate: DateOnly.FromDateTime(UtcNow).AddYears(-10));
        context.Favorites.Add(Favorite.CreateForMovie(userId, movieId, UtcNow));
        await context.SaveChangesAsync();

        var maps = await CreateRelevanceRepository(context)
            .GetMovieChangesRefreshMapsForTmdbIdsAsync([82001]);

        Assert.Equal(movieId, maps.UserRelevantByTmdbId[82001]);
        Assert.Equal(movieId, maps.UnionByTmdbId[82001]);
    }

    [Fact]
    public async Task ChangesRefreshMapsDiscoveryRelevantChangedIdIsSelected()
    {
        await using var context = await CreateIsolatedContextAsync();
        var movieId = await SeedMovieAsync(
            context,
            tmdbId: 82002,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-40));

        var maps = await CreateRelevanceRepository(context)
            .GetMovieChangesRefreshMapsForTmdbIdsAsync([82002]);

        Assert.Equal(movieId, maps.DiscoveryRelevantByTmdbId[82002]);
        Assert.Equal(movieId, maps.UnionByTmdbId[82002]);
    }

    [Fact]
    public async Task ChangesRefreshMapsIrrelevantChangedIdIsSkipped()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(context, tmdbId: 82003, voteCount: 5, releaseDate: DateOnly.FromDateTime(UtcNow).AddYears(-10));

        var maps = await CreateRelevanceRepository(context)
            .GetMovieChangesRefreshMapsForTmdbIdsAsync([82003]);

        Assert.Empty(maps.UnionByTmdbId);
    }

    [Fact]
    public async Task ChangesRefreshMapsUnrelatedDiscoveryRelevantNotReturnedWhenNotInChangedSet()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(
            context,
            tmdbId: 82004,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-40));
        await SeedMovieAsync(
            context,
            tmdbId: 82005,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-40));

        var maps = await CreateRelevanceRepository(context)
            .GetMovieChangesRefreshMapsForTmdbIdsAsync([82004]);

        Assert.Single(maps.DiscoveryRelevantByTmdbId);
        Assert.Contains(82004, maps.DiscoveryRelevantByTmdbId.Keys);
        Assert.DoesNotContain(82005, maps.DiscoveryRelevantByTmdbId.Keys);
    }

    [Fact]
    public async Task SafetyNetNullFreshnessDiscoveryRelevantIsSelected()
    {
        await using var context = await CreateIsolatedContextAsync();
        var movieId = await SeedMovieAsync(
            context,
            tmdbId: 83001,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-30),
            metadataUpdatedAtUtc: null);

        var ids = await CreateFreshnessRepository(context)
            .SelectStaleDiscoveryRelevantMovieIdsAsync(UtcNow.AddHours(-72), 10);

        Assert.Contains(movieId, ids);
    }

    [Fact]
    public async Task SafetyNetStaleFreshnessIsSelected()
    {
        await using var context = await CreateIsolatedContextAsync();
        var movieId = await SeedMovieAsync(
            context,
            tmdbId: 83002,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-30),
            metadataUpdatedAtUtc: UtcNow.AddDays(-10));

        var ids = await CreateFreshnessRepository(context)
            .SelectStaleDiscoveryRelevantMovieIdsAsync(UtcNow.AddHours(-72), 10);

        Assert.Contains(movieId, ids);
    }

    [Fact]
    public async Task SafetyNetFreshMetadataIsNotSelected()
    {
        await using var context = await CreateIsolatedContextAsync();
        var movieId = await SeedMovieAsync(
            context,
            tmdbId: 83003,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-30),
            metadataUpdatedAtUtc: UtcNow.AddHours(-1));

        var ids = await CreateFreshnessRepository(context)
            .SelectStaleDiscoveryRelevantMovieIdsAsync(UtcNow.AddHours(-72), 10);

        Assert.DoesNotContain(movieId, ids);
    }

    [Fact]
    public async Task SafetyNetDiscoveryIrrelevantIsNotSelected()
    {
        await using var context = await CreateIsolatedContextAsync();
        var movieId = await SeedMovieAsync(
            context,
            tmdbId: 83004,
            voteCount: 5,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddYears(-10),
            metadataUpdatedAtUtc: null);

        var ids = await CreateFreshnessRepository(context)
            .SelectStaleDiscoveryRelevantMovieIdsAsync(UtcNow.AddHours(-72), 10);

        Assert.DoesNotContain(movieId, ids);
    }

    [Fact]
    public async Task SafetyNetRespectsTakeLimit()
    {
        await using var context = await CreateIsolatedContextAsync();
        for (var i = 0; i < 5; i++)
        {
            await SeedMovieAsync(
                context,
                tmdbId: 83100 + i,
                voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
                releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-30),
                metadataUpdatedAtUtc: null);
        }

        var ids = await CreateFreshnessRepository(context)
            .SelectStaleDiscoveryRelevantMovieIdsAsync(UtcNow.AddHours(-72), 3);

        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task SafetyNetServiceRespectsMaxItemsPerRun()
    {
        await using var context = await CreateIsolatedContextAsync();
        for (var i = 0; i < 5; i++)
        {
            await SeedMovieAsync(
                context,
                tmdbId: 83200 + i,
                voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
                releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-30),
                metadataUpdatedAtUtc: null);
        }

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
                FreshnessThresholdHours = 72
            }),
            NullLogger<CatalogMetadataFreshnessSafetyNetService>.Instance);

        var result = await safetyNet.RunAsync();

        Assert.Equal(2, result.SelectedBatchCount);
        Assert.Equal(result.SelectedBatchCount, recording.RefreshedMovieIds.Count);
    }

    [Fact]
    public async Task FreshnessDistributionMatchesFixtureBuckets()
    {
        await using var context = await CreateIsolatedContextAsync();
        await SeedMovieAsync(
            context,
            tmdbId: 84001,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-20),
            metadataUpdatedAtUtc: UtcNow.AddHours(-12));
        await SeedMovieAsync(
            context,
            tmdbId: 84002,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-20),
            metadataUpdatedAtUtc: UtcNow.AddHours(-48));
        await SeedMovieAsync(
            context,
            tmdbId: 84003,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-20),
            metadataUpdatedAtUtc: UtcNow.AddDays(-10));
        await SeedMovieAsync(
            context,
            tmdbId: 84004,
            voteCount: PopularDiscoverQuality.MinimumVoteCountMovie,
            releaseDate: DateOnly.FromDateTime(UtcNow).AddDays(-20),
            metadataUpdatedAtUtc: null);

        var distribution = await CreateFreshnessRepository(context)
            .GetDiscoveryFreshnessDistributionAsync(UtcNow);

        Assert.Equal(4, distribution.DiscoveryRelevantTotal);
        Assert.Equal(1, distribution.FreshWithin24Hours);
        Assert.Equal(1, distribution.Fresh24To72Hours);
        Assert.Equal(2, distribution.StaleOver72Hours);
        Assert.Equal(2, distribution.StaleOver7Days);
        Assert.Equal(1, distribution.NeverRefreshed);
    }

    private static async Task<ApplicationDbContext> CreateIsolatedContextAsync()
    {
        var context = CatalogPersistenceFixture.CreateContext();
        context.Movies.RemoveRange(context.Movies);
        context.Favorites.RemoveRange(context.Favorites);
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
        return context;
    }

    private static async Task<Guid> SeedMovieAsync(
        ApplicationDbContext context,
        int tmdbId,
        int voteCount,
        DateOnly releaseDate,
        Guid? movieId = null,
        DateTime? metadataUpdatedAtUtc = null,
        DateTime? providerSeenAtUtc = null)
    {
        var id = movieId ?? Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = id,
            TmdbId = tmdbId,
            Title = $"Movie {tmdbId}",
            VoteCount = voteCount,
            VoteAverage = 7m,
            ReleaseDate = releaseDate,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            TmdbMetadataUpdatedAtUtc = metadataUpdatedAtUtc,
            TmdbProviderDiscoverySeenAtUtc = providerSeenAtUtc
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static CatalogChangesRelevanceRepository CreateRelevanceRepository(ApplicationDbContext context) =>
        new(
            context,
            Options.Create(new NewReleasesOptions { MaxAgeDays = 90, MinVoteCountMovie = 75, MinVoteCountTv = 50 }),
            Options.Create(new RecommendationOptions { CandidateMinVoteCount = 20 }),
            Options.Create(new TopRatedOptions()),
            Options.Create(new CatalogMetadataFreshnessOptions()));

    private static CatalogMetadataFreshnessRepository CreateFreshnessRepository(ApplicationDbContext context) =>
        new(
            context,
            Options.Create(new NewReleasesOptions { MaxAgeDays = 90, MinVoteCountMovie = 75, MinVoteCountTv = 50 }),
            Options.Create(new RecommendationOptions { CandidateMinVoteCount = 20 }),
            Options.Create(new TopRatedOptions()),
            Options.Create(new CatalogMetadataFreshnessOptions()));

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
