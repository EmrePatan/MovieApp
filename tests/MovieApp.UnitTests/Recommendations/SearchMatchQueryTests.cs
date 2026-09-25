using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class SearchMatchQueryTests
{
    [Fact]
    public void BestMovieTitleMatches_TakesTopCandidatesPerQuery()
    {
        using var context = CreateContext();

        var sql = UserRecommendationContextLoader.BestMovieTitleMatches(
            context.Movies.AsNoTracking(),
            ["dune", "alien"]).ToQueryString();

        var limitCount = sql.Split("LIMIT", StringSplitOptions.None).Length - 1;
        Assert.True(limitCount >= 2, sql);
        Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=movieapp;Username=postgres;Password=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }
}
