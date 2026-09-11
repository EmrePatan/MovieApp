using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Ratings;

public sealed class RatingRepositoryTests
{
    [Fact]
    public async Task GetSummaryForMovieAsyncCalculatesAverageCountAndDistribution()
    {
        var movieId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();

        await using var context = CreateContext();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Test Movie",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        context.Ratings.AddRange(
            Rating.CreateForMovie(userA, movieId, 8, DateTime.UtcNow),
            Rating.CreateForMovie(userB, movieId, 10, DateTime.UtcNow),
            Rating.CreateForMovie(userC, movieId, 6, DateTime.UtcNow));

        await context.SaveChangesAsync();

        var repository = new RatingRepository(context);
        var summary = await repository.GetSummaryForMovieAsync(movieId);

        Assert.Equal(3, summary.RatingCount);
        Assert.Equal(8m, summary.AverageScore);
        Assert.Equal(1, summary.ScoreDistribution[6]);
        Assert.Equal(1, summary.ScoreDistribution[8]);
        Assert.Equal(1, summary.ScoreDistribution[10]);
        Assert.Equal(0, summary.ScoreDistribution[1]);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"rating-repository-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
