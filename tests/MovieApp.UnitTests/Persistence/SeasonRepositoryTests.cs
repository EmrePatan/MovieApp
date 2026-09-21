using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Persistence;

public sealed class SeasonRepositoryTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncPreservesSeasonIdAndPreventsDuplicateEpisodes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"season-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowAsync(context);
        var repository = new SeasonRepository(context);
        var details = CreateSeasonDetails();

        var created = await repository.UpsertFromProviderAsync(tvShowId, details);
        var updated = await repository.UpsertFromProviderAsync(
            tvShowId,
            details with { Overview = "Updated overview" });

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(1, await context.Seasons.CountAsync());
        Assert.Equal(2, await context.Episodes.CountAsync());
    }

    [Fact]
    public async Task UpsertSeasonsFromProviderAsync_DetectsExistingDatabaseEpisodeWhenNavigationIsEmpty()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"season-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowAsync(context);
        var seasonId = Guid.NewGuid();
        var existingEpisodeId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            EpisodeCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        context.Episodes.Add(new Episode
        {
            Id = existingEpisodeId,
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Existing",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SeasonRepository(context);
        var details = CreateSeasonDetails() with
        {
            Episodes =
            [
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900301, null, "tt900301", 1, 1, "Pilot", "Pilot overview",
                    new DateOnly(2008, 1, 20), 58, "/fake/s1e1.jpg", 8.2m, 100),
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900302, null, "tt900302", 1, 2, "Episode 2", "Episode 2 overview",
                    new DateOnly(2008, 1, 27), 48, "/fake/s1e2.jpg", 8.1m, 90)
            ]
        };

        await repository.UpsertSeasonsFromProviderAsync(tvShowId, [details]);

        Assert.Equal(1, await context.Seasons.CountAsync());
        Assert.Equal(2, await context.Episodes.CountAsync());
        var updatedEpisode = await context.Episodes.SingleAsync(episode => episode.EpisodeNumber == 1);
        Assert.Equal(existingEpisodeId, updatedEpisode.Id);
        Assert.Equal("Pilot", updatedEpisode.Name);
    }

    [Fact]
    public async Task UpsertSeasonsFromProviderAsync_DeduplicatesDuplicateEpisodeNumbersInProviderPayload()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"season-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowAsync(context);
        var repository = new SeasonRepository(context);
        var duplicateEpisode = new EpisodeProviderDetails(
            "fake-tv-900101", 900301, null, "tt900301", 1, 1, "Pilot", "Pilot overview",
            new DateOnly(2008, 1, 20), 58, "/fake/s1e1.jpg", 8.2m, 100);
        var details = CreateSeasonDetails() with
        {
            Episodes = [duplicateEpisode, duplicateEpisode with { Name = "Pilot duplicate" }]
        };

        await repository.UpsertSeasonsFromProviderAsync(tvShowId, [details]);

        Assert.Equal(1, await context.Episodes.CountAsync(episode => episode.EpisodeNumber == 1));
        Assert.Equal("Pilot duplicate", await context.Episodes.Where(episode => episode.EpisodeNumber == 1).Select(episode => episode.Name).SingleAsync());
    }

    [Fact]
    public async Task UpsertSeasonsFromProviderAsync_BatchUpsertAcrossMultipleSeasonsIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"season-repository-{Guid.NewGuid()}")
            .Options;

        await using var context = new ApplicationDbContext(options);
        var tvShowId = await SeedTvShowAsync(context);
        var repository = new SeasonRepository(context);
        var seasonOne = CreateSeasonDetails();
        var seasonTwo = CreateSeasonDetails() with
        {
            SeasonNumber = 2,
            TmdbId = 900202,
            Name = "Season 2",
            Episodes =
            [
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900401, null, "tt900401", 2, 1, "S2E1", "Overview",
                    new DateOnly(2009, 3, 8), 48, "/fake/s2e1.jpg", 8.0m, 80)
            ]
        };

        await repository.UpsertSeasonsFromProviderAsync(tvShowId, [seasonOne, seasonTwo]);
        await repository.UpsertSeasonsFromProviderAsync(tvShowId, [seasonOne, seasonTwo]);

        Assert.Equal(2, await context.Seasons.CountAsync());
        Assert.Equal(3, await context.Episodes.CountAsync());
    }

    private static async Task<Guid> SeedTvShowAsync(ApplicationDbContext context)
    {
        var tvShow = new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = 900101,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TvShows.Add(tvShow);
        await context.SaveChangesAsync();
        return tvShow.Id;
    }

    private static SeasonProviderDetails CreateSeasonDetails() =>
        new(
            "fake-tv-900101",
            TmdbId: 900201,
            TvdbId: null,
            SeasonNumber: 1,
            Name: "Season 1",
            Overview: "Overview",
            AirDate: new DateOnly(2008, 1, 20),
            EpisodeCount: 2,
            PosterPath: "/fake/s1.jpg",
            Episodes:
            [
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900301, null, "tt900301", 1, 1, "Pilot", "Pilot overview",
                    new DateOnly(2008, 1, 20), 58, "/fake/s1e1.jpg", 8.2m, 100),
                new EpisodeProviderDetails(
                    "fake-tv-900101", 900302, null, "tt900302", 1, 2, "Episode 2", "Episode 2 overview",
                    new DateOnly(2008, 1, 27), 48, "/fake/s1e2.jpg", 8.1m, 90)
            ]);
}
