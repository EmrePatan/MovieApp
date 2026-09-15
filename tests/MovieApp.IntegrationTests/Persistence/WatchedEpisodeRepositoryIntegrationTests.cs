using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class WatchedEpisodeRepositoryIntegrationTests
{
    [Fact]
    public async Task GetContinueWatchingTvShowsAsyncIncludesPartiallyWatchedShowAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (tvShowId, watchedEpisodeId, _) = await SeedTvShowWithEpisodesAsync(context, episodeCount: 3);
        await SeedWatchedEpisodeAsync(context, userId, watchedEpisodeId, DateTime.UtcNow.AddHours(-1));

        var repository = new WatchedEpisodeRepository(context);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.Single(items);
        Assert.Equal(tvShowId, items[0].TvShowId);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsyncExcludesFullyWatchedShowAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedFullyWatchedTvShowAsync(context, userId, episodeCount: 2);

        var repository = new WatchedEpisodeRepository(context);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.DoesNotContain(items, item => item.TvShowId == tvShowId);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsyncDoesNotDuplicateShowForMultipleWatchedEpisodes()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedTvShowEpisodeIdsAsync(context, episodeCount: 4);
        await SeedWatchedEpisodeAsync(context, userId, episodeIds[0], DateTime.UtcNow.AddHours(-3));
        await SeedWatchedEpisodeAsync(context, userId, episodeIds[1], DateTime.UtcNow.AddHours(-1));

        var repository = new WatchedEpisodeRepository(context);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.Single(items);
        Assert.Equal(tvShowId, items[0].TvShowId);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsyncOrdersByMostRecentlyWatchedAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (olderShowId, olderEpisodeId, _) = await SeedTvShowWithEpisodesAsync(
            context,
            episodeCount: 2,
            titlePrefix: "Older");
        var (newerShowId, newerEpisodeId, _) = await SeedTvShowWithEpisodesAsync(
            context,
            episodeCount: 2,
            titlePrefix: "Newer");

        await SeedWatchedEpisodeAsync(context, userId, olderEpisodeId, DateTime.UtcNow.AddDays(-2));
        await SeedWatchedEpisodeAsync(context, userId, newerEpisodeId, DateTime.UtcNow.AddHours(-1));

        var repository = new WatchedEpisodeRepository(context);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.Equal(2, items.Count);
        Assert.Equal(newerShowId, items[0].TvShowId);
        Assert.Equal(olderShowId, items[1].TvShowId);
        Assert.True(items[0].LastWatchedAt > items[1].LastWatchedAt);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsyncRespectsTakeLimitAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();

        for (var index = 0; index < 4; index++)
        {
            var (_, episodeId, _) = await SeedTvShowWithEpisodesAsync(
                context,
                episodeCount: 2,
                titlePrefix: $"Show-{index}");
            await SeedWatchedEpisodeAsync(
                context,
                userId,
                episodeId,
                DateTime.UtcNow.AddHours(-index));
        }

        var repository = new WatchedEpisodeRepository(context);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 2);

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsyncTranslatesWithoutClientEvaluationAgainstPostgreSql()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (_, episodeId, _) = await SeedTvShowWithEpisodesAsync(context, episodeCount: 2);

        await SeedWatchedEpisodeAsync(context, userId, episodeId, DateTime.UtcNow);

        var repository = new WatchedEpisodeRepository(context);
        var query = context.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .GroupBy(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Select(group => new
            {
                TvShowId = group.Key,
                LastWatchedAt = group.Max(watchedEpisode => watchedEpisode.WatchedAt)
            });

        var sql = query.ToQueryString();
        Assert.DoesNotContain("client", sql, StringComparison.OrdinalIgnoreCase);

        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 5);
        Assert.Single(items);
    }

    private static async Task<(Guid TvShowId, Guid WatchedEpisodeId, IReadOnlyList<Guid> EpisodeIds)> SeedTvShowWithEpisodesAsync(
        ApplicationDbContext context,
        int episodeCount,
        string titlePrefix = "Continue")
    {
        var (tvShowId, episodeIds) = await SeedTvShowEpisodeIdsAsync(context, episodeCount, titlePrefix);
        return (tvShowId, episodeIds[0], episodeIds);
    }

    private static async Task<(Guid TvShowId, IReadOnlyList<Guid> EpisodeIds)> SeedTvShowEpisodeIdsAsync(
        ApplicationDbContext context,
        int episodeCount,
        string titlePrefix = "Continue")
    {
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeIds = new List<Guid>();

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = $"{titlePrefix} {Guid.NewGuid():N}",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        for (var episodeNumber = 1; episodeNumber <= episodeCount; episodeNumber++)
        {
            var episodeId = Guid.NewGuid();
            episodeIds.Add(episodeId);
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = episodeNumber,
                Name = $"Episode {episodeNumber}",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();
        return (tvShowId, episodeIds);
    }

    private static async Task<(Guid TvShowId, IReadOnlyList<Guid> EpisodeIds)> SeedFullyWatchedTvShowAsync(
        ApplicationDbContext context,
        Guid userId,
        int episodeCount)
    {
        var (tvShowId, episodeIds) = await SeedTvShowEpisodeIdsAsync(context, episodeCount);
        var watchedAt = DateTime.UtcNow;

        foreach (var episodeId in episodeIds)
        {
            await SeedWatchedEpisodeAsync(context, userId, episodeId, watchedAt);
        }

        return (tvShowId, episodeIds);
    }

    private static async Task SeedUserAsync(ApplicationDbContext context, Guid userId)
    {
        var utcNow = DateTime.UtcNow;
        var email = $"user-{userId:N}@example.com";
        context.Users.Add(new User
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            Email = email,
            NormalizedEmail = email,
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedWatchedEpisodeAsync(
        ApplicationDbContext context,
        Guid userId,
        Guid episodeId,
        DateTime watchedAt)
    {
        if (!await context.Users.AnyAsync(user => user.Id == userId))
        {
            await SeedUserAsync(context, userId);
        }

        context.WatchedEpisodes.Add(new WatchedEpisode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EpisodeId = episodeId,
            WatchedAt = watchedAt,
            CreatedAt = watchedAt,
            UpdatedAt = watchedAt
        });
        await context.SaveChangesAsync();
    }
}
