using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Search;

public sealed class CatalogMeanVoteAverageCacheTests
{
    [Fact]
    public async Task GetCatalogMeanVoteAverageAsync_ReusesCachedMeanUntilTtlExpires()
    {
        await using var context = CreateContext();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Cached Mean",
            VoteAverage = 8m,
            VoteCount = 100,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            cache);

        var first = await repository.GetCatalogMeanVoteAverageAsync(SearchContentType.Movie);
        movie.VoteAverage = 2m;
        await context.SaveChangesAsync();
        var second = await repository.GetCatalogMeanVoteAverageAsync(SearchContentType.Movie);

        Assert.Equal(8m, first);
        Assert.Equal(first, second);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-mean-cache-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
