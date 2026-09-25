using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryWatchedQueryTests
{
    [Fact]
    public void WatchedUnionPageIsTranslatedWithDatabaseLimitAndOffset()
    {
        using var context = CreateContext();
        var repository = new LibraryRepository(context);
        var userId = Guid.NewGuid();

        var sql = repository.WatchedUnionRows(userId)
            .OrderByDescending(row => row.LastActivityAt)
            .ThenBy(row => row.Type)
            .ThenBy(row => row.Id)
            .Skip(24)
            .Take(24)
            .ToQueryString();

        Assert.Contains("watched_movies", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tv_shows", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompletedTvShowPageIsTranslatedWithDatabaseLimit()
    {
        using var context = CreateContext();
        var repository = new LibraryRepository(context);

        var sql = repository.CompletedTvShowRows(Guid.NewGuid())
            .OrderByDescending(row => row.LastActivityAt)
            .ThenBy(row => row.Id)
            .Take(24)
            .ToQueryString();

        Assert.Contains("tv_shows", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("watched_episodes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Id\" IN", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("watched_movies", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }
}
