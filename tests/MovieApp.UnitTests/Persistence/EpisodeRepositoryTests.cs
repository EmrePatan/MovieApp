using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class EpisodeRepositoryTests
{
    [Fact]
    public async Task GetEpisodeCountsBySeasonAsyncReturnsCountsOrderedBySeasonNumber()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"episode-counts-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowWithSeasonsAsync(
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
    public async Task GetEpisodeCountsBySeasonAsyncReturnsEmptyListForShowWithoutEpisodes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"episode-counts-empty-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowWithSeasonsAsync(context);
        var repository = new EpisodeRepository(context);

        var counts = await repository.GetEpisodeCountsBySeasonAsync(tvShowId);

        Assert.Empty(counts);
    }

    [Fact]
    public async Task GetFirstUnwatchedForTvShowAsyncIgnoresSeasonZero()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"first-unwatched-tv-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        var (tvShowId, season0EpisodeId, season1EpisodeId) = await SeedTvShowWithTrackedEpisodesAsync(
            context,
            userId,
            [(seasonNumber: 0, episodeCount: 2), (seasonNumber: 1, episodeCount: 3)]);
        var repository = new EpisodeRepository(context);

        var nextEpisode = await repository.GetFirstUnwatchedForTvShowAsync(tvShowId, userId);

        Assert.NotNull(nextEpisode);
        Assert.Equal(season1EpisodeId, nextEpisode!.Id);
        Assert.Equal(1, nextEpisode.Season.SeasonNumber);
        Assert.Equal(1, nextEpisode.EpisodeNumber);
    }

    [Fact]
    public async Task GetFirstUnwatchedForTvShowAsyncReturnsNullWhenOnlySeasonZeroRemainsUnwatched()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"first-unwatched-tv-complete-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        var (tvShowId, _, _) = await SeedTvShowWithTrackedEpisodesAsync(
            context,
            userId,
            [(seasonNumber: 0, episodeCount: 2), (seasonNumber: 1, episodeCount: 2)],
            markRegularSeasonsWatched: true);
        var repository = new EpisodeRepository(context);

        var nextEpisode = await repository.GetFirstUnwatchedForTvShowAsync(tvShowId, userId);

        Assert.Null(nextEpisode);
    }

    [Fact]
    public async Task GetFirstUnwatchedForSeasonAsyncStillReturnsSeasonZeroEpisodeWhenRequested()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"first-unwatched-season-zero-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        var (tvShowId, season0EpisodeId, _) = await SeedTvShowWithTrackedEpisodesAsync(
            context,
            userId,
            [(seasonNumber: 0, episodeCount: 2), (seasonNumber: 1, episodeCount: 2)]);
        var repository = new EpisodeRepository(context);

        var nextEpisode = await repository.GetFirstUnwatchedForSeasonAsync(tvShowId, 0, userId);

        Assert.NotNull(nextEpisode);
        Assert.Equal(season0EpisodeId, nextEpisode!.Id);
        Assert.Equal(1, nextEpisode.EpisodeNumber);
    }

    [Fact]
    public async Task UpsertFromProviderAsyncPreservesEpisodeIdForDuplicateExternalData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"episode-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var seasonId = await SeedSeasonAsync(context);
        var repository = new EpisodeRepository(context);
        var details = CreateEpisodeDetails();

        var created = await repository.UpsertFromProviderAsync(seasonId, details);
        var updated = await repository.UpsertFromProviderAsync(
            seasonId,
            details with { VoteCount = 250, Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(250, updated.VoteCount);
        Assert.Equal(1, await context.Episodes.CountAsync());
    }

    private static async Task<Guid> SeedTvShowWithSeasonsAsync(
        ApplicationDbContext context,
        params (int seasonNumber, int episodeCount)[] seasons)
    {
        var utcNow = DateTime.UtcNow;
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Breaking Bad",
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
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                });
            }
        }

        await context.SaveChangesAsync();
        return tvShow.Id;
    }

    private static async Task<(Guid TvShowId, Guid Season0EpisodeId, Guid Season1EpisodeId)>
        SeedTvShowWithTrackedEpisodesAsync(
            ApplicationDbContext context,
            Guid userId,
            (int seasonNumber, int episodeCount)[] seasons,
            bool markRegularSeasonsWatched = false)
    {
        var utcNow = DateTime.UtcNow;
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Breaking Bad",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        context.TvShows.Add(tvShow);
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@example.com",
            UserName = $"user-{userId:N}",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        Guid? season0EpisodeId = null;
        Guid? season1EpisodeId = null;

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
                var episode = new Episode
                {
                    Id = Guid.NewGuid(),
                    SeasonId = season.Id,
                    Season = season,
                    EpisodeNumber = episodeNumber,
                    Name = $"Episode {episodeNumber}",
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                };

                context.Episodes.Add(episode);

                if (seasonNumber == 0 && episodeNumber == 1)
                {
                    season0EpisodeId = episode.Id;
                }

                if (seasonNumber == 1 && episodeNumber == 1)
                {
                    season1EpisodeId = episode.Id;
                }
            }
        }

        await context.SaveChangesAsync();

        if (markRegularSeasonsWatched)
        {
            var regularEpisodeIds = await context.Episodes
                .Where(episode => episode.Season.TvShowId == tvShow.Id && episode.Season.SeasonNumber >= 1)
                .Select(episode => episode.Id)
                .ToListAsync();

            foreach (var episodeId in regularEpisodeIds)
            {
                context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodeId, utcNow));
            }

            await context.SaveChangesAsync();
        }

        return (tvShow.Id, season0EpisodeId!.Value, season1EpisodeId!.Value);
    }

    private static async Task<Guid> SeedSeasonAsync(ApplicationDbContext context)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = 900101,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var season = new Season
        {
            Id = Guid.NewGuid(),
            TvShowId = tvShow.Id,
            TvShow = tvShow,
            SeasonNumber = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season.Id;
    }

    private static EpisodeProviderDetails CreateEpisodeDetails() =>
        new(
            "fake-tv-900101",
            TmdbId: 900301,
            TvdbId: null,
            ImdbId: "tt900301",
            SeasonNumber: 1,
            EpisodeNumber: 1,
            Name: "Pilot",
            Overview: "Pilot overview",
            AirDate: new DateOnly(2008, 1, 20),
            RuntimeMinutes: 58,
            StillPath: "/fake/s1e1.jpg",
            VoteAverage: 8.2m,
            VoteCount: 100);
}
