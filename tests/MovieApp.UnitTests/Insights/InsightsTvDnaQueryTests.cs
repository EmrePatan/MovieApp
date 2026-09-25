using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsTvDnaQueryTests
{
    [Fact]
    public void TvShowDnaTitles_StartFromTheUsersWatchedEpisodes()
    {
        using var context = CreateContext();
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var sql = new InsightsRepository(context, null!).GetTvShowDnaTitlesSql(userId);

        Assert.Contains("watched_episodes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Id\" IN", sql, StringComparison.Ordinal);
        Assert.Contains(userId.ToString(), sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }
}
