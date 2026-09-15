using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Services.HotRelease;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.IntegrationTests.HotRelease;

[CollectionDefinition("HotRelease")]
public sealed class HotReleaseTestsDefinition : ICollectionFixture<HotReleaseFixture>;

[Collection("HotRelease")]
public sealed class HotReleaseIntegrationTests(HotReleaseFixture fixture)
{
    private static readonly DateOnly Boundary = new(2026, 9, 15);
    private static readonly DateOnly BeforeBoundary = new(2026, 9, 14);

    [Fact]
    public async Task KnownFutureEpisodeReleasesWhenBoundaryCrosses()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithEpisodeAsync(Boundary);
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        var before = await service.RunAsync(BeforeBoundary);
        var onBoundary = await service.RunAsync(Boundary);

        Assert.Equal(0, before.ReleaseEventsCreated);
        Assert.True(onBoundary.ReleaseEventsCreated > 0);

        await using var context = HotReleaseFixture.CreateContext();
        Assert.All(
            await context.CatalogReleaseEvents.ToListAsync(),
            releaseEvent => Assert.Equal(CatalogReleaseEventSource.BoundaryDetection, releaseEvent.Source));
    }

    [Fact]
    public async Task KnownFutureEpisodeDoesNotNeedProviderRefresh()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithEpisodeAsync(Boundary);
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        await service.RunAsync(Boundary);

        Assert.Equal(0, tracker.GetTvShowCallCount);
        Assert.Equal(0, tracker.GetSeasonCallCount);
    }

    [Fact]
    public async Task MultipleFollowersProduceSingleHotCandidate()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithEpisodeAsync(Boundary);
        await SeedFollowAsync(tvShowId, "a");
        await SeedFollowAsync(tvShowId, "b");

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        var result = await service.RunAsync(Boundary);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Checked);
        Assert.Equal(0, tracker.GetTvShowCallCount);
    }

    [Fact]
    public async Task PartialHotSeasonHydratesOnlyRequiredSeason()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedPartialSeasonShowAsync();
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        var result = await service.RunAsync(Boundary);

        Assert.Equal(1, result.Hydrated);
        Assert.Equal(1, tracker.GetSeasonCallCount);
        Assert.Equal(0, tracker.GetTvShowCallCount);
    }

    [Fact]
    public async Task SeasonZeroNeverEntersHotSet()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedSeasonZeroShowAsync(Boundary);
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        var result = await service.RunAsync(Boundary);

        Assert.Equal(0, result.Candidates);
        Assert.Equal(0, result.ReleaseEventsCreated);
    }

    [Fact]
    public async Task HotProviderRefreshUsesHotReleaseReason()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedPartialSeasonShowAsync();
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        await service.RunAsync(Boundary);

        await using var context = HotReleaseFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(TvShowCatalogRefreshReason.HotRelease, syncState.LastRefreshReason);
        Assert.NotNull(syncState.LastRefreshedAtUtc);
    }

    [Fact]
    public async Task NextHotCheckTracksNextKnownRelease()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithEpisodesAsync(
            CreateEpisode(5, Boundary),
            CreateEpisode(6, new DateOnly(2026, 9, 18)));
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        await service.RunAsync(Boundary);

        await using var context = HotReleaseFixture.CreateContext();
        var syncState = await context.TvShowCatalogSyncStates.SingleAsync();
        Assert.Equal(
            ReleaseDateTime.ToReleaseAtUtc(new DateOnly(2026, 9, 18)),
            syncState.NextHotCheckAtUtc);
    }

    [Fact]
    public async Task RepeatedHotChecksDoNotDuplicateEvents()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedShowWithEpisodeAsync(Boundary);
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        await service.RunAsync(Boundary);
        var firstCount = await CountReleaseEventsAsync();
        await service.RunAsync(Boundary);
        var secondCount = await CountReleaseEventsAsync();

        Assert.Equal(firstCount, secondCount);
        Assert.True(firstCount > 0);
    }

    [Fact]
    public async Task FailedHotCandidateRemainsEligibleForRetry()
    {
        await fixture.ResetAsync();

        var tvShowId = await SeedPartialSeasonShowAsync();
        await SeedFollowAsync(tvShowId);

        using var scope = fixture.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().FailGetSeason = true;
        var service = scope.ServiceProvider.GetRequiredService<IHotReleaseCheckService>();

        var failedRun = await service.RunAsync(Boundary);

        Assert.Equal(1, failedRun.Candidates);
        Assert.Equal(0, failedRun.Checked);
        Assert.Equal(1, failedRun.Failures);

        scope.ServiceProvider.GetRequiredService<TvShowDataProviderCallTracker>().FailGetSeason = false;
        fixture.ResetFakes();

        var retry = await service.RunAsync(Boundary);

        Assert.Equal(1, retry.Candidates);
        Assert.Equal(1, retry.Checked);
        Assert.Equal(0, retry.Failures);
        Assert.Equal(1, retry.Hydrated);
    }

    private static async Task<Guid> SeedShowWithEpisodeAsync(DateOnly airDate) =>
        await SeedShowWithEpisodesAsync(CreateEpisode(5, airDate));

    private static async Task<Guid> SeedShowWithEpisodesAsync(params Episode[] episodes)
    {
        await using var context = HotReleaseFixture.CreateContext();
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
            EpisodeCount = episodes.Length,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        foreach (var episode in episodes)
        {
            episode.SeasonId = seasonId;
            episode.Id = Guid.NewGuid();
            episode.CreatedAt = DateTime.UtcNow;
            episode.UpdatedAt = DateTime.UtcNow;
            context.Episodes.Add(episode);
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedPartialSeasonShowAsync(int tmdbId = FakeTvShowDataProvider.BreakingBadTmdbId)
    {
        await using var context = HotReleaseFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = tmdbId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            AirDate = new DateOnly(2026, 8, 1),
            EpisodeCount = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Episodes.Add(new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = 1,
            AirDate = Boundary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static async Task<Guid> SeedSeasonZeroShowAsync(DateOnly airDate)
    {
        await using var context = HotReleaseFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = FakeTvShowDataProvider.BreakingBadTmdbId,
            Title = "Specials",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 0,
            AirDate = airDate,
            EpisodeCount = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Episodes.Add(new Episode
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = 1,
            AirDate = airDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return tvShowId;
    }

    private static Episode CreateEpisode(int episodeNumber, DateOnly airDate) =>
        new()
        {
            EpisodeNumber = episodeNumber,
            AirDate = airDate
        };

    private static async Task SeedFollowAsync(Guid tvShowId, string userSuffix = "primary")
    {
        await using var context = HotReleaseFixture.CreateContext();
        var user = User.Create(
            Guid.NewGuid(),
            $"{userSuffix}@example.com",
            "hash",
            $"Hot Release {userSuffix}",
            DateTime.UtcNow);
        context.Users.Add(user);
        var follow = CatalogFollow.CreateTvFollow(user.Id, tvShowId, true, true, DateTime.UtcNow);
        follow.SetNotifyFromUtc(DateTime.UtcNow, DateTime.UtcNow);
        follow.EstablishBaseline(DateTime.UtcNow);
        context.CatalogFollows.Add(follow);
        await context.SaveChangesAsync();
    }

    private static async Task<int> CountReleaseEventsAsync()
    {
        await using var context = HotReleaseFixture.CreateContext();
        return await context.CatalogReleaseEvents.CountAsync();
    }
}
