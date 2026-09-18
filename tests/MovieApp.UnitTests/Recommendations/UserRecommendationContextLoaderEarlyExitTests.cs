using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class UserRecommendationContextLoaderEarlyExitTests
{
    private const int PersonalizationThreshold = 3;

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
            minimumInteractionsForEnrichment: PersonalizationThreshold);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
        Assert.Empty(recommendationContext.ExcludedMovieIds);
        Assert.Empty(recommendationContext.ExcludedTvShowIds);
    }

    [Fact]
    public async Task GetUserRecommendationContextAsyncShortCircuitsWithTwoDistinctMeaningfulInteractions()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var movieIds = Enumerable.Range(0, 2)
            .Select(_ => Guid.NewGuid())
            .ToList();

        context.Users.Add(User.Create(
            userId,
            "warming@example.com",
            "hash",
            "Warming User",
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
            minimumInteractionsForEnrichment: PersonalizationThreshold);

        Assert.Equal(2, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
        Assert.Empty(recommendationContext.ExcludedMovieIds);
        Assert.Empty(recommendationContext.ExcludedTvShowIds);
    }

    [Fact]
    public async Task GetUserRecommendationContextAsyncCollapsesCrossSourceDuplicatesBeforeThresholdCheck()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var movieId = Guid.NewGuid();

        context.Users.Add(User.Create(
            userId,
            "duplicate@example.com",
            "hash",
            "Duplicate User",
            utcNow));
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Shared Movie",
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
        context.Ratings.Add(new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            Score = 9,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(
            userId,
            minimumInteractionsForEnrichment: PersonalizationThreshold);

        Assert.Equal(1, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
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
            minimumInteractionsForEnrichment: PersonalizationThreshold);

        Assert.Equal(3, recommendationContext.MeaningfulInteractionCount);
        Assert.Equal(3, recommendationContext.Signals.Count);
        Assert.Equal(3, recommendationContext.ExcludedMovieIds.Count);
    }

    [Fact]
    public async Task GetUserRecommendationContextAsyncShortCircuitsSearchOnlyUsersWhenThresholdIsSet()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        context.Users.Add(User.Create(
            userId,
            "search-only-threshold@example.com",
            "hash",
            "Search User",
            utcNow));
        context.SearchHistories.Add(SearchHistory.Create(
            userId,
            "Inception",
            "inception",
            utcNow));
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(
            userId,
            minimumInteractionsForEnrichment: PersonalizationThreshold);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"recommendation-loader-early-exit-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }
}
