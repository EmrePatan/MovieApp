using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Search;

public sealed class CatalogMeanVoteAverageWeightedMeanTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(5_000)]
    [InlineData(150_000)]
    public async Task GetCatalogMeanVoteAverageAsync_ComputesVoteWeightedMean(int dominantVoteCount)
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        context.Movies.AddRange(
            new Movie
            {
                Id = Guid.NewGuid(),
                Title = "Established Hit",
                VoteAverage = 8m,
                VoteCount = dominantVoteCount,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = Guid.NewGuid(),
                Title = "Niche Favorite",
                VoteAverage = 6m,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions()),
            NullLogger<SearchRepository>.Instance);

        var mean = await repository.GetCatalogMeanVoteAverageAsync(SearchContentType.Movie);

        var weightedSum = (double)8m * dominantVoteCount + (double)6m * 100;
        var expectedMean = (decimal)(weightedSum / (dominantVoteCount + 100));
        Assert.Equal(expectedMean, mean);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-mean-weighted-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
