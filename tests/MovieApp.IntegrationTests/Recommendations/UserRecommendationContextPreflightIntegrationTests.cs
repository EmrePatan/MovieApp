using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.IntegrationTests.Persistence;

namespace MovieApp.IntegrationTests.Recommendations;

[Collection("CatalogPersistence")]
public sealed class UserRecommendationContextPreflightIntegrationTests
{
    private const int PersonalizationThreshold = 3;

    [Fact]
    public async Task PreflightShortCircuitsZeroInteractionUsersWithoutSnapshotQueries()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        await SeedUserAsync(context, userId);

        var recommendationContext = await LoadWithThresholdAsync(context, userId);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
        Assert.Empty(recommendationContext.ExcludedMovieIds);
        Assert.Empty(recommendationContext.ExcludedTvShowIds);
    }

    [Fact]
    public async Task PreflightShortCircuitsTwoDistinctMeaningfulInteractions()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        await SeedUserAsync(context, userId);

        foreach (var title in new[] { "Alpha", "Beta" })
        {
            var movie = CreateMovie(tmdbId: NextTmdbId(), title);
            context.Movies.Add(movie);
            context.Favorites.Add(new Favorite
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movie.Id,
                CreatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();

        var recommendationContext = await LoadWithThresholdAsync(context, userId);

        Assert.Equal(2, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
    }

    [Fact]
    public async Task PreflightAllowsFullLoadForThreeDistinctMeaningfulInteractions()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        await SeedUserAsync(context, userId);

        foreach (var title in new[] { "One", "Two", "Three" })
        {
            var movie = CreateMovie(tmdbId: NextTmdbId(), title);
            context.Movies.Add(movie);
            context.Favorites.Add(new Favorite
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movie.Id,
                CreatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();

        var recommendationContext = await LoadWithThresholdAsync(context, userId);

        Assert.Equal(3, recommendationContext.MeaningfulInteractionCount);
        Assert.Equal(3, recommendationContext.Signals.Count);
        Assert.Equal(3, recommendationContext.ExcludedMovieIds.Count);
    }

    [Fact]
    public async Task PreflightCollapsesCrossSourceDuplicatesToOneMeaningfulInteraction()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var movie = CreateMovie(tmdbId: NextTmdbId());
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        context.Favorites.Add(new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movie.Id,
            CreatedAt = utcNow
        });
        context.Ratings.Add(new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movie.Id,
            Score = 9,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        await context.SaveChangesAsync();

        var recommendationContext = await LoadWithThresholdAsync(context, userId);

        Assert.Equal(1, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
    }

    [Fact]
    public async Task PreflightCountsMultipleWatchedEpisodesAsOneTvShow()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedTvShowEpisodeIdsAsync(context, episodeCount: 3);
        await SeedUserAsync(context, userId);

        foreach (var episodeId in episodeIds)
        {
            await SeedWatchedEpisodeAsync(context, userId, episodeId, DateTime.UtcNow);
        }

        var recommendationContext = await LoadWithThresholdAsync(context, userId);

        Assert.Equal(1, recommendationContext.MeaningfulInteractionCount);
        Assert.Empty(recommendationContext.Signals);
    }

    [Fact]
    public async Task PreflightProbeMatchesLoaderCountSemanticsWithoutThreshold()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var movie = CreateMovie(tmdbId: NextTmdbId());
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        context.Favorites.Add(new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movie.Id,
            CreatedAt = utcNow
        });
        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(userId, movie.Id, utcNow));
        context.SearchHistories.Add(SearchHistory.Create(
            userId,
            movie.Title,
            movie.Title.ToLowerInvariant(),
            utcNow));
        await context.SaveChangesAsync();

        var probeCount = await UserRecommendationContextInteractionProbe.CountDistinctMeaningfulInteractionsAsync(
            context,
            userId,
            CancellationToken.None);

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(userId);

        Assert.Equal(probeCount, recommendationContext.MeaningfulInteractionCount);
        Assert.Equal(1, probeCount);
        Assert.Contains(
            recommendationContext.Signals,
            signal => signal.SignalType == UserBehaviorSignalTypes.Search);
        Assert.Contains(movie.Id, recommendationContext.ExcludedMovieIds);
    }

    [Fact]
    public async Task SearchOnlyUsersStillLoadSearchSignalsWhenThresholdIsZero()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movie = CreateMovie(tmdbId: NextTmdbId(), title: "Inception Search Match");
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        context.SearchHistories.Add(SearchHistory.Create(
            userId,
            "Inception",
            "inception",
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(userId);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
        Assert.Contains(
            recommendationContext.Signals,
            signal => signal.SignalType == UserBehaviorSignalTypes.Search);
    }

    [Fact]
    public async Task MovieFollowExclusionsRemainAvailableWhenThresholdIsZero()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movie = CreateMovie(tmdbId: NextTmdbId());
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);

        context.CatalogFollows.Add(CatalogFollow.CreateMovieFollow(userId, movie.Id, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(userId);

        Assert.Equal(0, recommendationContext.MeaningfulInteractionCount);
        Assert.Contains(movie.Id, recommendationContext.ExcludedMovieIds);
        Assert.DoesNotContain(
            recommendationContext.Signals,
            signal => signal.ContentId == movie.Id);
    }

    private static async Task<UserRecommendationContext> LoadWithThresholdAsync(
        ApplicationDbContext context,
        Guid userId)
    {
        var repository = new RecommendationRepository(context);
        return await repository.GetUserRecommendationContextAsync(
            userId,
            minimumInteractionsForEnrichment: PersonalizationThreshold);
    }

    private static int _nextTmdbId = 80_000;

    private static int NextTmdbId() => Interlocked.Increment(ref _nextTmdbId);

    private static Movie CreateMovie(int tmdbId, string title = "Sample Movie")
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

    private static async Task<(Guid TvShowId, IReadOnlyList<Guid> EpisodeIds)> SeedTvShowEpisodeIdsAsync(
        ApplicationDbContext context,
        int episodeCount)
    {
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeIds = new List<Guid>();

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = NextTmdbId(),
            Title = "Watched Show",
            Overview = "Overview",
            FirstAirDate = new DateOnly(2020, 1, 1),
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = 8m,
            VoteCount = 500,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            TmdbId = NextTmdbId(),
            SeasonNumber = 1,
            Name = "Season 1",
            EpisodeCount = episodeCount,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        for (var index = 0; index < episodeCount; index++)
        {
            var episodeId = Guid.NewGuid();
            episodeIds.Add(episodeId);
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                TmdbId = NextTmdbId(),
                EpisodeNumber = index + 1,
                Name = $"Episode {index + 1}",
                AirDate = new DateOnly(2020, 1, index + 1),
                VoteAverage = 8m,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
        }

        await context.SaveChangesAsync();
        return (tvShowId, episodeIds);
    }

    private static async Task SeedWatchedEpisodeAsync(
        ApplicationDbContext context,
        Guid userId,
        Guid episodeId,
        DateTime watchedAt)
    {
        context.WatchedEpisodes.Add(new WatchedEpisode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EpisodeId = episodeId,
            WatchedAt = watchedAt,
            CreatedAt = watchedAt,
            UpdatedAt = watchedAt
        });
        await context.SaveChangesAsync();
    }
}
