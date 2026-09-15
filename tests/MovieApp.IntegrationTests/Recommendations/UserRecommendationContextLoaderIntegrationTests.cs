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
public sealed class UserRecommendationContextLoaderIntegrationTests
{
    [Fact]
    public async Task MeaningfulInteractionCountCollapsesMultipleWatchedEpisodesToOneTvTitle()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var (tvShowId, episodeIds) = await SeedTvShowEpisodeIdsAsync(context, episodeCount: 3);
        await SeedUserAsync(context, userId);

        foreach (var episodeId in episodeIds)
        {
            await SeedWatchedEpisodeAsync(context, userId, episodeId, DateTime.UtcNow);
        }

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(userId);

        Assert.Equal(1, recommendationContext.MeaningfulInteractionCount);
        Assert.Single(
            recommendationContext.Signals,
            signal => signal.ContentType == "tv" &&
                      signal.ContentId == tvShowId &&
                      signal.SignalType == UserBehaviorSignalTypes.Watched);
    }

    [Fact]
    public async Task SearchHistoryDoesNotSatisfyColdStartThreshold()
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
    public async Task MovieFollowExcludesTitleWithoutPositiveTasteOrThresholdCredit()
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

    [Fact]
    public async Task TvFollowCountsTowardThresholdAndExcludesFollowedTitle()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var tvShow = CreateTvShow(tmdbId: NextTmdbId());
        context.TvShows.Add(tvShow);
        await SeedUserAsync(context, userId);

        context.CatalogFollows.Add(CatalogFollow.CreateTvFollow(
            userId,
            tvShow.Id,
            notifyNewSeasons: true,
            notifyNewEpisodes: true,
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(userId);

        Assert.Equal(1, recommendationContext.MeaningfulInteractionCount);
        Assert.Contains(tvShow.Id, recommendationContext.ExcludedTvShowIds);
        Assert.Contains(
            recommendationContext.Signals,
            signal => signal.ContentId == tvShow.Id &&
                      signal.SignalType == UserBehaviorSignalTypes.TvFollow);
    }

    [Fact]
    public async Task DuplicateSignalsForSameTitleDoNotInflateMeaningfulInteractionCount()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = Guid.NewGuid();
        var movie = CreateMovie(tmdbId: NextTmdbId());
        context.Movies.Add(movie);
        await SeedUserAsync(context, userId);
        var utcNow = DateTime.UtcNow;

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

        var repository = new RecommendationRepository(context);
        var recommendationContext = await repository.GetUserRecommendationContextAsync(userId);

        Assert.Equal(1, recommendationContext.MeaningfulInteractionCount);
        Assert.Contains(
            recommendationContext.Signals,
            signal => signal.ContentId == movie.Id &&
                      signal.SignalType == UserBehaviorSignalTypes.Favorite);
    }

    [Fact]
    public async Task ContextLoadingIsIsolatedPerUser()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var movie = CreateMovie(tmdbId: NextTmdbId());
        context.Movies.Add(movie);
        await SeedUserAsync(context, userA);
        await SeedUserAsync(context, userB);

        context.Favorites.Add(new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userA,
            MovieId = movie.Id,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repository = new RecommendationRepository(context);
        var contextA = await repository.GetUserRecommendationContextAsync(userA);
        var contextB = await repository.GetUserRecommendationContextAsync(userB);

        Assert.Equal(1, contextA.MeaningfulInteractionCount);
        Assert.Equal(0, contextB.MeaningfulInteractionCount);
        Assert.Contains(movie.Id, contextA.ExcludedMovieIds);
        Assert.DoesNotContain(movie.Id, contextB.ExcludedMovieIds);
    }

    private static int _nextTmdbId = 70_000;

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

    private static TvShow CreateTvShow(int tmdbId, string title = "Sample Show")
    {
        var utcNow = DateTime.UtcNow;

        return new TvShow
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = title,
            Overview = "Overview",
            FirstAirDate = new DateOnly(2020, 1, 1),
            Status = TvShowStatus.ReturningSeries,
            VoteAverage = 7.5m,
            VoteCount = 1000,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
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
