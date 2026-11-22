using MovieApp.Application.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Recommendations;

[Collection("CatalogPersistence")]
public sealed class RecommendationDismissalIntegrationTests
{
    // Keep this class in a distinct range from SimilarCandidateIdBatchLoaderIntegrationTests;
    // both share the CatalogPersistence database for the lifetime of the collection.
    private static int _nextTmdbId = 9_100_000;

    [Fact]
    public async Task DismissedMovieIsExcludedAndBecomesDismissedSignal()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movie = CreateMovie(Interlocked.Increment(ref _nextTmdbId));
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        var repository = new RecommendationDismissalRepository(context);
        Assert.True(await repository.TryAddAsync(
            RecommendationDismissal.CreateForMovie(userId, movie.Id, DateTime.UtcNow)));

        var recommendationContext = await new RecommendationRepository(context)
            .GetUserRecommendationContextAsync(userId);

        Assert.Contains(movie.Id, recommendationContext.ExcludedMovieIds);
        Assert.Contains(
            recommendationContext.Signals,
            signal => signal.ContentId == movie.Id &&
                      signal.SignalType == UserBehaviorSignalTypes.Dismissed);
    }

    [Fact]
    public async Task DismissalDoesNotCountTowardPersonalizationThreshold()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movie = CreateMovie(Interlocked.Increment(ref _nextTmdbId));
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        await new RecommendationDismissalRepository(context).TryAddAsync(
            RecommendationDismissal.CreateForMovie(userId, movie.Id, DateTime.UtcNow));

        var recommendationContext = await new RecommendationRepository(context)
            .GetUserRecommendationContextAsync(userId);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
    }

    [Fact]
    public async Task RepeatedDismissalIsRejectedByUniqueIndex()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movie = CreateMovie(Interlocked.Increment(ref _nextTmdbId));
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        var repository = new RecommendationDismissalRepository(context);
        var first = await repository.TryAddAsync(
            RecommendationDismissal.CreateForMovie(userId, movie.Id, DateTime.UtcNow));
        var second = await repository.TryAddAsync(
            RecommendationDismissal.CreateForMovie(userId, movie.Id, DateTime.UtcNow));

        Assert.True(first);
        Assert.False(second);
        Assert.True(await repository.ExistsForMovieAsync(userId, movie.Id));
    }

    private static Movie CreateMovie(int tmdbId, string title = "Dismiss Sample Movie")
    {
        var utcNow = DateTime.UtcNow;

        return new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = title,
            OriginalTitle = title,
            Overview = "Overview",
            ReleaseDate = new DateOnly(2020, 1, 1),
            VoteAverage = 7.5m,
            VoteCount = 1000,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static async Task SeedUserAsync(ApplicationDbContext context, Guid userId)
    {
        var utcNow = DateTime.UtcNow;
        var email = $"user-{userId:N}@example.com";
        context.Users.Add(new User
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();
    }
}
