using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class ContinueWatchingQueryTests
{
    [Fact]
    public void ContinueWatchingQuery_LimitsToShowsTheUserHasWatched()
    {
        using var context = CreateContext();
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var sql = new WatchedEpisodeRepository(context).GetContinueWatchingSql(userId, take: 10);

        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("watched_episodes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"TvShowId\" IN", sql, StringComparison.Ordinal);
        Assert.Contains(userId.ToString(), sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }
}
