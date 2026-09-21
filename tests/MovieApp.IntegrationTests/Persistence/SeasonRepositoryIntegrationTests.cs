using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class SeasonRepositoryIntegrationTests
{
    [Fact]
    public async Task UpsertFromProviderAsyncCompletesUnderNpgsqlRetryExecutionStrategy()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = 910_003,
            Title = "Watch History Show",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SeasonRepository(context);
        var details = CreateSeasonDetails(tvShowId, seasonNumber: 1, episodeCount: 2);

        var season = await repository.UpsertFromProviderAsync(tvShowId, details);

        Assert.Equal(1, season.SeasonNumber);
        Assert.Equal(2, await context.Episodes.CountAsync(episode => episode.SeasonId == season.Id));
    }

    [Fact]
    public async Task UpsertSeasonsFromProviderAsyncCompletesUnderNpgsqlRetryExecutionStrategy()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = 910_004,
            Title = "Batch Hydration Show",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SeasonRepository(context);
        var details = new[]
        {
            CreateSeasonDetails(tvShowId, seasonNumber: 1, episodeCount: 2),
            CreateSeasonDetails(tvShowId, seasonNumber: 2, episodeCount: 1)
        };

        await repository.UpsertSeasonsFromProviderAsync(tvShowId, details);

        Assert.Equal(2, await context.Seasons.CountAsync(season => season.TvShowId == tvShowId));
        Assert.Equal(3, await context.Episodes.CountAsync(episode => episode.Season.TvShowId == tvShowId));
    }

    [Fact]
    public async Task UpsertSeasonsFromProviderAsyncPreventsDuplicateEpisodeInsertWhenEpisodeAlreadyExists()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = 910_001,
            Title = "Late Night Show",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

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
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Existing Episode",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new SeasonRepository(context);
        var details = new SeasonProviderDetails(
            "fake-tv-910001",
            910_101,
            null,
            1,
            "Season 1",
            "Overview",
            new DateOnly(2003, 1, 26),
            2,
            null,
            [
                new EpisodeProviderDetails(
                    "fake-tv-910001", 910_201, null, null, 1, 1, "Episode 1", "Overview",
                    new DateOnly(2003, 1, 26), 45, null, 7.5m, 10),
                new EpisodeProviderDetails(
                    "fake-tv-910001", 910_202, null, null, 1, 2, "Episode 2", "Overview",
                    new DateOnly(2003, 1, 27), 45, null, 7.4m, 9)
            ]);

        await repository.UpsertSeasonsFromProviderAsync(tvShowId, [details]);

        Assert.Equal(1, await context.Seasons.CountAsync(season => season.TvShowId == tvShowId));
        Assert.Equal(2, await context.Episodes.CountAsync(episode => episode.SeasonId == seasonId));
    }

    [Fact]
    public async Task UpsertSeasonsFromProviderAsyncConcurrentCallsDoNotViolateEpisodeUniqueIndex()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var tvShowId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = 910_002,
            Title = "Concurrent Late Night Show",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();

        var details = new SeasonProviderDetails(
            "fake-tv-910002",
            910_102,
            null,
            1,
            "Season 1",
            "Overview",
            new DateOnly(2003, 1, 26),
            2,
            null,
            [
                new EpisodeProviderDetails(
                    "fake-tv-910002", 910_301, null, null, 1, 1, "Episode 1", "Overview",
                    new DateOnly(2003, 1, 26), 45, null, 7.5m, 10),
                new EpisodeProviderDetails(
                    "fake-tv-910002", 910_302, null, null, 1, 2, "Episode 2", "Overview",
                    new DateOnly(2003, 1, 27), 45, null, 7.4m, 9)
            ]);

        var tasks = Enumerable.Range(0, 4)
            .Select(async _ =>
            {
                await using var scopedContext = CatalogPersistenceFixture.CreateContext();
                var repository = new SeasonRepository(scopedContext);
                await repository.UpsertSeasonsFromProviderAsync(tvShowId, [details]);
            })
            .ToArray();

        await Task.WhenAll(tasks);

        await using var verifyContext = CatalogPersistenceFixture.CreateContext();
        Assert.Equal(1, await verifyContext.Seasons.CountAsync(season => season.TvShowId == tvShowId));
        Assert.Equal(2, await verifyContext.Episodes.CountAsync(episode => episode.Season.TvShowId == tvShowId));
    }

    private static SeasonProviderDetails CreateSeasonDetails(
        Guid tvShowId,
        int seasonNumber,
        int episodeCount)
    {
        var episodes = Enumerable.Range(1, episodeCount)
            .Select(episodeNumber => new EpisodeProviderDetails(
                $"fake-tv-{tvShowId:N}",
                920_000 + seasonNumber * 100 + episodeNumber,
                null,
                null,
                seasonNumber,
                episodeNumber,
                $"Episode {episodeNumber}",
                "Overview",
                new DateOnly(2003, 1, 26).AddDays(episodeNumber - 1),
                45,
                null,
                7.5m,
                10))
            .ToList();

        return new SeasonProviderDetails(
            $"fake-tv-{tvShowId:N}",
            910_000 + seasonNumber,
            null,
            seasonNumber,
            $"Season {seasonNumber}",
            "Overview",
            new DateOnly(2003, 1, 26),
            episodeCount,
            null,
            episodes);
    }
}
