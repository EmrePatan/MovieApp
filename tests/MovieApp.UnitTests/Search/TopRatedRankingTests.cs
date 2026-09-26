using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.UnitTests.Search;

public sealed class TopRatedRankingTests
{
    [Fact]
    public void ComputeWeightedRatingSuppressesTinyVoteCountPerfectRatings()
    {
        const decimal catalogMean = 6.5m;
        const int minimumVoteConfidence = 100;

        var obscurePerfect = TopRatedScoreCalculator.ComputeWeightedRating(10m, 2, catalogMean, minimumVoteConfidence);
        var establishedHit = TopRatedScoreCalculator.ComputeWeightedRating(8.5m, 500, catalogMean, minimumVoteConfidence);

        Assert.True(establishedHit > obscurePerfect);
    }

    [Fact]
    public async Task GetTopRatedAsyncUsesDeterministicTieBreaking()
    {
        await using var context = CreateContext();
        var utcNow = DateTime.UtcNow;
        var movieA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var movieB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        context.Movies.AddRange(
            new Movie
            {
                Id = movieB,
                Title = "Bravo Film",
                VoteAverage = 8m,
                VoteCount = 200,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new Movie
            {
                Id = movieA,
                Title = "Alpha Film",
                VoteAverage = 8m,
                VoteCount = 200,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        await context.SaveChangesAsync();

        var repository = new SearchRepository(
            context,
            Options.Create(new TopRatedOptions { MinimumVoteConfidence = 100 }),
            NullLogger<SearchRepository>.Instance);

        var result = await repository.GetTopRatedAsync(new DiscoveryCriteria(
            SearchContentType.Movie,
            1,
            10));

        Assert.Equal(movieA, result.Items[0].Id);
        Assert.Equal(movieB, result.Items[1].Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"top-rated-ranking-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
