using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryWatchedQueryTests
{
    [Fact]
    public void StartedShowsQueryUsesIndexedLookupsInsteadOfAGlobalEpisodeAggregate()
    {
        using var context = CreateContext();

        var sql = TvShowCompletionQueries.StartedShows(context, Guid.NewGuid()).ToQueryString();

        Assert.Contains("WITH watched AS MATERIALIZED", sql, StringComparison.Ordinal);
        Assert.Contains("LATERAL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("episode_rows", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }
}
