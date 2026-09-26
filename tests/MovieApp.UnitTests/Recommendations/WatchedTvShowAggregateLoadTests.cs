using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class WatchedTvShowAggregateLoadTests
{
    [Fact]
    public async Task LoadWatchedTvShowAggregatesAsync_ReturnsOneRowPerShowWithLatestWatchedAt()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var showA = Guid.NewGuid();
        var showB = Guid.NewGuid();
        var older = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var latestA = older.AddDays(10);
        var latestB = older.AddDays(1);

        await SeedShowWithWatchedEpisodesAsync(context, userId, showA, "Show A", episodeCount: 50, watchedAt: older, latestWatch: latestA);
        await SeedShowWithWatchedEpisodesAsync(context, userId, showB, "Show B", episodeCount: 30, watchedAt: older, latestWatch: latestB);

        var metrics = new RecommendationQueryMetrics();
        var rows = await UserRecommendationContextInteractionLoader.LoadWatchedTvShowAggregatesAsync(
            context,
            userId,
            metrics,
            CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal(latestA, rows.Single(row => row.TvShowId == showA).WatchedAt);
        Assert.Equal(latestB, rows.Single(row => row.TvShowId == showB).WatchedAt);

        var titles = new Dictionary<Guid, string>
        {
            [showA] = "Show A",
            [showB] = "Show B"
        };
        var seeds = UserRecommendationContextLoader.CreateWatchedTvShowSeeds(rows, titles);
        Assert.Equal(2, seeds.Count);
        Assert.All(seeds, seed => Assert.Equal(UserBehaviorSignalTypes.Watched, seed.SignalType));
    }

    [Fact]
    public void CountMeaningfulInteractions_MatchesDistinctShowsFromAggregatedRows()
    {
        var showId = Guid.NewGuid();
        var rows = new List<UserRecommendationContextModels.WatchedEpisodeRow>
        {
            new(showId, DateTime.UtcNow)
        };

        var count = UserRecommendationContextLoader.CountMeaningfulInteractions(
            [],
            [],
            [],
            [],
            rows,
            []);

        Assert.Equal(1, count);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"watched-tv-aggregates-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedShowWithWatchedEpisodesAsync(
        ApplicationDbContext context,
        Guid userId,
        Guid tvShowId,
        string title,
        int episodeCount,
        DateTime watchedAt,
        DateTime latestWatch)
    {
        var utcNow = DateTime.UtcNow;
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = Random.Shared.Next(1_000_000, 9_000_000),
            Title = title,
            Overview = "Overview",
            FirstAirDate = new DateOnly(2020, 1, 1),
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = 8m,
            VoteCount = 100,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            TmdbId = Random.Shared.Next(1_000_000, 9_000_000),
            SeasonNumber = 1,
            Name = "Season 1",
            EpisodeCount = episodeCount,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        for (var index = 0; index < episodeCount; index++)
        {
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                TmdbId = Random.Shared.Next(1_000_000, 9_000_000),
                EpisodeNumber = index + 1,
                Name = $"Episode {index + 1}",
                AirDate = new DateOnly(2020, 1, 1),
                VoteAverage = 8m,
                VoteCount = 10,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

            var at = index == episodeCount - 1 ? latestWatch : watchedAt;
            context.WatchedEpisodes.Add(new WatchedEpisode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EpisodeId = episodeId,
                WatchedAt = at,
                CreatedAt = at,
                UpdatedAt = at
            });
        }

        await context.SaveChangesAsync();
    }
}
