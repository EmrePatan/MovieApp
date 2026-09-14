using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Services.TvShowChanges;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.TvShowChanges;

[CollectionDefinition("TmdbTvChangesSync")]
public sealed class TmdbTvChangesSyncCollection : ICollectionFixture<TmdbTvChangesSyncFixture>;

[Collection("TmdbTvChangesSync")]
public sealed class TmdbTvChangesSyncIntegrationTests(TmdbTvChangesSyncFixture fixture)
{
    private static readonly DateTime SyncInstant = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly WindowStart = new(2026, 9, 14);
    private static readonly DateOnly WindowEnd = new(2026, 9, 15);

    [Fact]
    public async Task ChangedUnfollowedShowIsIgnored()
    {
        await fixture.ResetAsync();

        await SeedTvShowAsync(FakeTvShowDataProvider.BreakingBadTmdbId, fullyHydrated: true);
        ConfigureChangesWindow(fixture, [999999]);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();

        var result = await syncService.SyncAsync(SyncInstant);

        Assert.Equal(0, result.FollowedShowsRefreshed);
        Assert.Equal(0, tracker.GetTvShowCallCount);

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        Assert.Equal(0, await context.TvShowCatalogSyncStates.CountAsync());
    }

    [Fact]
    public async Task ChangedFollowedShowIsRefreshedOnce()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedTvShowAsync(FakeTvShowDataProvider.BreakingBadTmdbId, fullyHydrated: true);
        await SeedFollowAsync(tvShowId);
        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();

        var result = await syncService.SyncAsync(SyncInstant);

        Assert.Equal(1, result.FollowedShowsRefreshed);
        Assert.Equal(1, tracker.GetTvShowCallCount);

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(tvShowId, syncState.TvShowId);
        Assert.Equal(TvShowCatalogRefreshReason.ChangesSync, syncState.LastRefreshReason);
    }

    [Fact]
    public async Task MultipleFollowersStillRefreshShowOnce()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedTvShowAsync(FakeTvShowDataProvider.BreakingBadTmdbId, fullyHydrated: true);
        await SeedFollowAsync(tvShowId, userSuffix: "a");
        await SeedFollowAsync(tvShowId, userSuffix: "b");
        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();

        var result = await syncService.SyncAsync(SyncInstant);

        Assert.Equal(1, result.FollowedShowsRefreshed);
        Assert.Equal(1, tracker.GetTvShowCallCount);
    }

    [Fact]
    public async Task ChangesSyncCreatesReleaseEventsViaPostRefresh()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithFutureEpisodeAsync();
        await SeedFollowAsync(tvShowId);
        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();

        await syncService.SyncAsync(SyncInstant);

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        Assert.True(await context.CatalogReleaseEvents.AnyAsync());
        Assert.All(
            await context.CatalogReleaseEvents.ToListAsync(),
            releaseEvent => Assert.Equal(CatalogReleaseEventSource.ProviderRefresh, releaseEvent.Source));
    }

    [Fact]
    public async Task FailedWindowDoesNotAdvanceCheckpoint()
    {
        await fixture.ResetAsync();

        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId], failPage: true);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => syncService.SyncAsync(SyncInstant));

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        Assert.Equal(0, await context.TmdbTvChangesSyncCheckpoints.CountAsync());
    }

    [Fact]
    public async Task FailedShowRefreshDoesNotAdvanceCheckpoint()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedTvShowAsync(FakeTvShowDataProvider.BreakingBadTmdbId, fullyHydrated: true);
        await SeedFollowAsync(tvShowId);
        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);

        using var scope = fixture.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().FailGetTvShow = true;
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => syncService.SyncAsync(SyncInstant));

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        Assert.Equal(0, await context.TmdbTvChangesSyncCheckpoints.CountAsync());
    }

    [Fact]
    public async Task SuccessfulWindowAdvancesCheckpoint()
    {
        await fixture.ResetAsync();

        ConfigureChangesWindow(fixture, []);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();

        var result = await syncService.SyncAsync(SyncInstant);

        Assert.Equal(WindowEnd, result.LastCompletedEndDate);

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        var checkpoint = await context.TmdbTvChangesSyncCheckpoints.SingleAsync();
        Assert.Equal(TmdbTvChangesSyncCheckpoint.DefaultCheckpointKey, checkpoint.CheckpointKey);
        Assert.Equal(WindowEnd, checkpoint.LastCompletedEndDate);
    }

    [Fact]
    public async Task ChangeSignalAndRefreshStateArePersisted()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedTvShowAsync(FakeTvShowDataProvider.BreakingBadTmdbId, fullyHydrated: true);
        await SeedFollowAsync(tvShowId);
        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();

        await syncService.SyncAsync(SyncInstant);

        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(WindowEnd, syncState.LastChangeSignalDate);
        Assert.NotNull(syncState.LastRefreshedAtUtc);
        Assert.Equal(TvShowCatalogRefreshReason.ChangesSync, syncState.LastRefreshReason);
    }

    [Fact]
    public async Task ReprocessingOverlapDoesNotDuplicateReleaseEvents()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithFutureEpisodeAsync();
        await SeedFollowAsync(tvShowId);
        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);

        using var scope = fixture.Factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ITmdbTvChangesSyncService>();

        await syncService.SyncAsync(SyncInstant);
        var eventCountAfterFirst = await CountReleaseEventsAsync();

        ConfigureChangesWindow(fixture, [FakeTvShowDataProvider.BreakingBadTmdbId]);
        await syncService.SyncAsync(SyncInstant);
        var eventCountAfterSecond = await CountReleaseEventsAsync();

        Assert.Equal(eventCountAfterFirst, eventCountAfterSecond);
        Assert.True(eventCountAfterFirst > 0);
    }

    private static void ConfigureChangesWindow(
        TmdbTvChangesSyncFixture fixture,
        int[] changedIds,
        bool failPage = false)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var changesProvider = scope.ServiceProvider.GetRequiredService<FakeTmdbTvChangesProvider>();
        changesProvider.Reset();
        changesProvider.FailNextPage = failPage;
        changesProvider.ConfigureWindow(WindowStart, WindowEnd, changedIds);
    }

    private static async Task<Guid> SeedTvShowAsync(int tmdbId, bool fullyHydrated)
    {
        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = tmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (fullyHydrated)
        {
            var season = new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                SeasonNumber = 1,
                AirDate = new DateOnly(2008, 1, 20),
                EpisodeCount = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Seasons.Add(season);

            for (var episodeNumber = 1; episodeNumber <= 3; episodeNumber++)
            {
                context.Episodes.Add(new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    EpisodeNumber = episodeNumber,
                    AirDate = new DateOnly(2008, 1, 20).AddDays(episodeNumber - 1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedShowWithFutureEpisodeAsync()
    {
        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            AirDate = new DateOnly(2008, 1, 20),
            EpisodeCount = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Episodes.Add(new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = 1,
            AirDate = WindowEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task SeedFollowAsync(Guid tvShowId, string userSuffix = "primary")
    {
        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        var user = User.Create(
            Guid.NewGuid(),
            $"{userSuffix}@example.com",
            "hash",
            $"Changes Sync {userSuffix}",
            DateTime.UtcNow);
        context.Users.Add(user);
        var follow = TvShowFollow.Create(user.Id, tvShowId, true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(DateTime.UtcNow, DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        context.TvShowFollows.Add(follow);
        await context.SaveChangesAsync();
    }

    private static async Task<int> CountReleaseEventsAsync()
    {
        await using var context = TmdbTvChangesSyncFixture.CreateContext();
        return await context.CatalogReleaseEvents.CountAsync();
    }
}
