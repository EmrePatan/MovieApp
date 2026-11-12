using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class EpisodeRepositoryIntegrationTests
{
    [Fact]
    public async Task GetEpisodeCountsBySeasonAsyncTranslatesAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = await SeedTvShowAsync(
            context,
            (seasonNumber: 0, episodeCount: 2),
            (seasonNumber: 1, episodeCount: 8),
            (seasonNumber: 2, episodeCount: 13));
        var repository = new EpisodeRepository(context);

        var counts = await repository.GetEpisodeCountsBySeasonAsync(tvShowId);

        Assert.Equal(3, counts.Count);
        Assert.Equal([0, 1, 2], counts.Select(count => count.SeasonNumber).ToList());
        Assert.Equal(2, counts[0].EpisodeCount);
        Assert.Equal(8, counts[1].EpisodeCount);
        Assert.Equal(13, counts[2].EpisodeCount);
    }

    [Fact]
    public async Task WatchProgressCountsOnlyReleasedEpisodesAndIgnoresFutureOrUnknownAirDates()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = await SeedTvShowAsync(context, (seasonNumber: 1, episodeCount: 3));
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"progress-{userId:N}@example.com",
            UserName = $"progress-{userId:N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var episodes = await context.Episodes
            .Where(episode => episode.Season.TvShowId == tvShowId)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToListAsync();
        var today = EpisodeWatchEligibility.TodayUtc();
        episodes[0].AirDate = today.AddDays(-1);
        episodes[1].AirDate = today.AddDays(1);
        episodes[2].AirDate = null;
        context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodes[0].Id, DateTime.UtcNow));
        context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodes[1].Id, DateTime.UtcNow));
        context.Seasons.AddRange(
            new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                SeasonNumber = 2,
                AirDate = today.AddDays(-30),
                EpisodeCount = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                SeasonNumber = 3,
                AirDate = today.AddDays(30),
                EpisodeCount = 6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        await context.SaveChangesAsync();

        var episodeRepository = new EpisodeRepository(context);
        var watchedRepository = new WatchedEpisodeRepository(
            context,
            NullLogger<WatchedEpisodeRepository>.Instance);

        var totals = await episodeRepository.GetEpisodeCountsBySeasonAsync(tvShowId);
        var watched = await watchedRepository.GetWatchedEpisodeCountsBySeasonAsync(userId, tvShowId);

        Assert.Equal(2, totals.Count);
        Assert.Equal(1, totals.Single(count => count.SeasonNumber == 1).EpisodeCount);
        Assert.Equal(5, totals.Single(count => count.SeasonNumber == 2).EpisodeCount);
        Assert.DoesNotContain(totals, count => count.SeasonNumber == 3);
        Assert.Single(watched);
        Assert.Equal(1, watched[0].EpisodeCount);
    }

    [Fact]
    public async Task GetFirstUnwatchedForTvShowAsyncIgnoresSeasonZeroAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var tvShowId = await SeedTvShowAsync(
            context,
            (seasonNumber: 0, episodeCount: 2),
            (seasonNumber: 1, episodeCount: 3));
        var repository = new EpisodeRepository(context);

        var nextEpisode = await repository.GetFirstUnwatchedForTvShowAsync(tvShowId, userId);

        Assert.NotNull(nextEpisode);
        Assert.Equal(1, nextEpisode!.Season.SeasonNumber);
        Assert.Equal(1, nextEpisode.EpisodeNumber);
    }

    [Fact]
    public async Task GetEpisodeCountsBySeasonAsyncReturnsEmptyListForShowWithoutEpisodes()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = await SeedTvShowAsync(context);
        var repository = new EpisodeRepository(context);

        var counts = await repository.GetEpisodeCountsBySeasonAsync(tvShowId);

        Assert.Empty(counts);
    }

    private static async Task<Guid> SeedTvShowAsync(
        ApplicationDbContext context,
        params (int seasonNumber, int episodeCount)[] seasons)
    {
        var utcNow = DateTime.UtcNow;
        var defaultAirDate = EpisodeWatchEligibility.TodayUtc().AddDays(-7);
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = $"Repository Show {Guid.NewGuid():N}",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        context.TvShows.Add(tvShow);

        foreach (var (seasonNumber, episodeCount) in seasons)
        {
            var season = new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShow.Id,
                TvShow = tvShow,
                SeasonNumber = seasonNumber,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            };

            context.Seasons.Add(season);

            for (var episodeNumber = 1; episodeNumber <= episodeCount; episodeNumber++)
            {
                context.Episodes.Add(new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    Season = season,
                    EpisodeNumber = episodeNumber,
                    Name = $"Episode {episodeNumber}",
                    AirDate = defaultAirDate.AddDays(episodeNumber),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                });
            }
        }

        await context.SaveChangesAsync();
        return tvShow.Id;
    }
}
