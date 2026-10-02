using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Persistence;

public sealed class CatalogMeanVoteAverageSqlTranslationTests
{
    [Fact]
    public void WeightedCatalogMeanQueryDoesNotCastVoteCountToVoteAverageNumericScale()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString())
            .Options;

        using var context = new ApplicationDbContext(options);
        var sql = context.Movies
            .AsNoTracking()
            .Where(movie => movie.VoteCount > 0)
            .Select(movie => (double)movie.VoteAverage * movie.VoteCount)
            .ToQueryString();

        Assert.DoesNotContain("numeric(5,2)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("double precision", sql, StringComparison.OrdinalIgnoreCase);
    }
}
