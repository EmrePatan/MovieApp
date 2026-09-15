using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.TvUpcomingEpisodes;

public sealed class TvUpcomingEpisodeSyncRepositoryTests
{
    [Fact]
    public async Task SelectStaleFollowedShowsAsync_ReturnsDistinctFollowedShowsWithStaleSync()
    {
        await using var context = CreateContext();
        var staleShowId = await SeedFollowedShowAsync(context, syncedAtUtc: DateTime.UtcNow.AddHours(-8));
        var freshShowId = await SeedFollowedShowAsync(context, syncedAtUtc: DateTime.UtcNow.AddHours(-1));
        await SeedFollowedShowAsync(context, syncedAtUtc: null, includeFollow: false);
        var repository = new TvUpcomingEpisodeSyncRepository(context);

        var candidates = await repository.SelectStaleFollowedShowsAsync(
            10,
            DateTime.UtcNow.AddHours(-6));

        Assert.Single(candidates);
        Assert.Equal(staleShowId, candidates[0].TvShowId);
        Assert.DoesNotContain(candidates, candidate => candidate.TvShowId == freshShowId);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tv-upcoming-sync-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Guid> SeedFollowedShowAsync(
        ApplicationDbContext context,
        DateTime? syncedAtUtc,
        bool includeFollow = true)
    {
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = Random.Shared.Next(1000, 9999),
            Title = "Show",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        if (syncedAtUtc.HasValue || syncedAtUtc is null)
        {
            context.TvShowCatalogSyncStates.Add(new TvShowCatalogSyncState
            {
                TvShowId = tvShowId,
                LastUpcomingEpisodeSyncAtUtc = syncedAtUtc,
                UpdatedAtUtc = utcNow
            });
        }

        if (includeFollow)
        {
            context.CatalogFollows.Add(CatalogFollow.CreateTvFollow(
                Guid.NewGuid(),
                tvShowId,
                true,
                true,
                utcNow));
        }

        await context.SaveChangesAsync();
        return tvShowId;
    }
}
