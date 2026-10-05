using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class WatchedEpisodeRepositoryContinueWatchingTests
{
    [Fact]
    public async Task GetContinueWatchingTvShowsAsync_IncludesShowWithAiredUnwatchedEpisode()
    {
        var today = EpisodeWatchEligibility.TodayUtc();
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedTvShowAsync(
            context,
            userId,
            [(1, today.AddDays(-1)), (2, today.AddDays(-2))]);
        await SeedWatchedEpisodeAsync(context, userId, episodeIds[0], DateTime.UtcNow.AddHours(-1));

        var repository = new WatchedEpisodeRepository(context, NullLogger<WatchedEpisodeRepository>.Instance);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.Single(items);
        Assert.Equal(tvShowId, items[0].TvShowId);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsync_ExcludesShowWhenOnlyFutureEpisodeRemainsUnwatched()
    {
        var today = EpisodeWatchEligibility.TodayUtc();
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedTvShowAsync(
            context,
            userId,
            [(1, today.AddDays(-1)), (2, today.AddDays(2))]);
        await SeedWatchedEpisodeAsync(context, userId, episodeIds[0], DateTime.UtcNow.AddHours(-1));

        var repository = new WatchedEpisodeRepository(context, NullLogger<WatchedEpisodeRepository>.Instance);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.DoesNotContain(items, item => item.TvShowId == tvShowId);
    }

    [Fact]
    public async Task GetContinueWatchingTvShowsAsync_ExcludesShowWhenOnlyNullAirDateEpisodeRemainsUnwatched()
    {
        var today = EpisodeWatchEligibility.TodayUtc();
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedTvShowAsync(
            context,
            userId,
            [(1, today.AddDays(-1)), (2, null)]);
        await SeedWatchedEpisodeAsync(context, userId, episodeIds[0], DateTime.UtcNow.AddHours(-1));

        var repository = new WatchedEpisodeRepository(context, NullLogger<WatchedEpisodeRepository>.Instance);
        var items = await repository.GetContinueWatchingTvShowsAsync(userId, take: 10);

        Assert.DoesNotContain(items, item => item.TvShowId == tvShowId);
    }

    [Fact]
    public void ContinueWatchingQuery_FiltersByAiredEpisodesInSql()
    {
        using var context = CreateNpgsqlContext();
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var sql = new WatchedEpisodeRepository(context, NullLogger<WatchedEpisodeRepository>.Instance)
            .GetContinueWatchingSql(userId, take: 10);

        Assert.Contains("AirDate", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"continue-watching-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ApplicationDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(Guid TvShowId, IReadOnlyList<Guid> EpisodeIds)> SeedTvShowAsync(
        ApplicationDbContext context,
        Guid userId,
        (int episodeNumber, DateOnly? airDate)[] episodes)
    {
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeIds = new List<Guid>();

        context.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@example.com",
            UserName = $"user-{userId:N}",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Continue Watching Show",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        foreach (var (episodeNumber, airDate) in episodes)
        {
            var episodeId = Guid.NewGuid();
            episodeIds.Add(episodeId);
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = episodeNumber,
                Name = $"Episode {episodeNumber}",
                AirDate = airDate,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
        }

        await context.SaveChangesAsync();
        return (tvShowId, episodeIds);
    }

    private static async Task SeedWatchedEpisodeAsync(
        ApplicationDbContext context,
        Guid userId,
        Guid episodeId,
        DateTime watchedAt)
    {
        context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodeId, watchedAt));
        await context.SaveChangesAsync();
    }
}
