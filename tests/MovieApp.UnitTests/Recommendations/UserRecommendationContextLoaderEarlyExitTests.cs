using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class UserRecommendationContextLoaderEarlyExitTests
{
    [Fact]
    public async Task GetUserRecommendationContextAsyncSkipsSignalEnrichmentWhenBelowPersonalizationThreshold()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Users.Add(User.Create(
            userId,
            "cold@example.com",
            "hash",
            "Cold User",
            utcNow));
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(
            userId,
            minimumInteractionsForEnrichment: 3);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
        Assert.Empty(recommendationContext.ExcludedMovieIds);
        Assert.Empty(recommendationContext.ExcludedTvShowIds);
    }

    [Fact]
    public async Task GetUserRecommendationContextAsyncPreservesEligiblePersonalizationBehavior()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var movieIds = Enumerable.Range(0, 3)
            .Select(_ => Guid.NewGuid())
            .ToList();

        context.Users.Add(User.Create(
            userId,
            "active@example.com",
            "hash",
            "Active User",
            utcNow));

        foreach (var movieId in movieIds)
        {
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = $"Movie {movieId}",
                VoteAverage = 8m,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

            context.Favorites.Add(new Favorite
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieId,
                CreatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(
            userId,
            minimumInteractionsForEnrichment: 3);

        Assert.Equal(3, recommendationContext.MeaningfulInteractionCount);
        Assert.Equal(3, recommendationContext.Signals.Count);
        Assert.Equal(3, recommendationContext.ExcludedMovieIds.Count);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recommendation-loader-early-exit-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
