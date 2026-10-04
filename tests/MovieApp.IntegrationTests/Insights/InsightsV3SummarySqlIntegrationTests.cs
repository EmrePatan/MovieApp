using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Ratings;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Insights;

public sealed class InsightsV3SummarySqlIntegrationTests
{
    [Fact]
    public async Task GetV3SummaryAsyncMatchesLegacyEfSemanticsAcrossScenarios()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        await AssertParityAsync(context, await SeedNoActivityAsync(context));
        await AssertParityAsync(context, await SeedMoviesOnlyAsync(context));
        await AssertParityAsync(context, await SeedSingleShowEpisodesAsync(context, episodeCount: 3));
        await AssertParityAsync(context, await SeedSameShowMultipleSeasonsAsync(context));
        await AssertParityAsync(context, await SeedMultipleShowsAsync(context));
        await AssertParityAsync(context, await SeedRatingsOnlyAsync(context));
        await AssertParityAsync(context, await SeedDuplicateMovieWatchesAsync(context));
        await AssertParityAsync(context, await SeedCombinedActivityAsync(context));
    }

    [Fact]
    public async Task GetV3SummaryAsyncShowsStartedEqualsDistinctSeriesCountForRepeatedEpisodes()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userId = await SeedSingleShowEpisodesAsync(context, episodeCount: 12);
        var summary = await InsightsV3SqlQueries.GetV3SummaryAsync(context, userId, CancellationToken.None);

        Assert.Equal(12, summary.EpisodesWatched);
        Assert.Equal(1, summary.ShowsStarted);
        Assert.Equal(1, summary.DistinctSeriesCount);
        Assert.Equal(summary.ShowsStarted, summary.DistinctSeriesCount);
    }

    private static async Task AssertParityAsync(ApplicationDbContext context, Guid userId)
    {
        var optimized = await InsightsV3SqlQueries.GetV3SummaryAsync(context, userId, CancellationToken.None);
        var legacy = await GetV3SummaryLegacyAsync(context, userId, CancellationToken.None);

        Assert.Equal(legacy.MemberSince, optimized.MemberSince);
        Assert.Equal(legacy.MoviesWatched, optimized.MoviesWatched);
        Assert.Equal(legacy.EpisodesWatched, optimized.EpisodesWatched);
        Assert.Equal(legacy.ShowsStarted, optimized.ShowsStarted);
        Assert.Equal(legacy.RatingsCount, optimized.RatingsCount);
        Assert.Equal(legacy.DistinctMovieCount, optimized.DistinctMovieCount);
        Assert.Equal(legacy.DistinctSeriesCount, optimized.DistinctSeriesCount);
    }

    private static async Task<InsightsV3SqlQueries.V3SummarySqlRow> GetV3SummaryLegacyAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new InsightsV3SqlQueries.V3SummarySqlRow
            {
                MemberSince = user.CreatedAt,
                MoviesWatched = context.WatchedMovies.Count(watchedMovie => watchedMovie.UserId == userId),
                EpisodesWatched = context.WatchedEpisodes.Count(watchedEpisode => watchedEpisode.UserId == userId),
                ShowsStarted = context.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count(),
                RatingsCount = context.Ratings.Count(rating => rating.UserId == userId),
                DistinctMovieCount = context.WatchedMovies
                    .Where(watchedMovie => watchedMovie.UserId == userId)
                    .Select(watchedMovie => watchedMovie.MovieId)
                    .Distinct()
                    .Count(),
                DistinctSeriesCount = context.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count(),
            })
            .FirstAsync(cancellationToken);
    }

    private static async Task<Guid> SeedNoActivityAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Users.Add(CreateUser(userId, utcNow));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMoviesOnlyAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        var movieA = Guid.NewGuid();
        var movieB = Guid.NewGuid();
        AddMovie(context, movieA, tmdbBase, utcNow);
        AddMovie(context, movieB, tmdbBase + 1, utcNow);

        context.WatchedMovies.AddRange(
            WatchedMovie(userId, movieA, utcNow, utcNow),
            WatchedMovie(userId, movieB, utcNow, utcNow.AddHours(1)));

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedSingleShowEpisodesAsync(ApplicationDbContext context, int episodeCount)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            TmdbId = tmdbBase,
            Title = "Single Show",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = showId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        for (var index = 0; index < episodeCount; index++)
        {
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = index + 1,
                Name = $"Episode {index + 1}",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(WatchedEpisode(userId, episodeId, utcNow.AddMinutes(index), utcNow));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedSameShowMultipleSeasonsAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        var showId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            TmdbId = tmdbBase,
            Title = "Multi Season Show",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        foreach (var seasonNumber in new[] { 1, 2 })
        {
            var seasonId = Guid.NewGuid();
            context.Seasons.Add(new Season
            {
                Id = seasonId,
                TvShowId = showId,
                SeasonNumber = seasonNumber,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = 1,
                Name = $"S{seasonNumber}E1",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(WatchedEpisode(userId, episodeId, utcNow, utcNow));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMultipleShowsAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        for (var showIndex = 0; showIndex < 3; showIndex++)
        {
            var showId = Guid.NewGuid();
            var seasonId = Guid.NewGuid();
            var episodeId = Guid.NewGuid();
            context.TvShows.Add(new TvShow
            {
                Id = showId,
                TmdbId = tmdbBase + showIndex,
                Title = $"Show {showIndex}",
                Status = TvShowStatus.Ended,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.Seasons.Add(new Season
            {
                Id = seasonId,
                TvShowId = showId,
                SeasonNumber = 1,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = 1,
                Name = "Pilot",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(WatchedEpisode(userId, episodeId, utcNow, utcNow));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedRatingsOnlyAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, tmdbBase, utcNow);
        context.Ratings.Add(new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            Score = 8,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedDuplicateMovieWatchesAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, tmdbBase, utcNow);

        // Domain allows only one row per (user, movie) — distinct movie count should remain 1.
        context.WatchedMovies.Add(WatchedMovie(userId, movieId, utcNow, utcNow));

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedCombinedActivityAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var tmdbBase = Random.Shared.Next(10_000_000, 90_000_000);
        context.Users.Add(CreateUser(userId, utcNow));

        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, tmdbBase, utcNow);
        context.WatchedMovies.Add(WatchedMovie(userId, movieId, utcNow, utcNow));

        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            TmdbId = tmdbBase + 1,
            Title = "Combo Show",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = showId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Episodes.Add(new Episode
        {
            Id = episodeId,
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Pilot",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.WatchedEpisodes.Add(WatchedEpisode(userId, episodeId, utcNow, utcNow));

        context.Ratings.Add(new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            Score = RatingScoreRules.MaxScore,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        await context.SaveChangesAsync();
        return userId;
    }

    private static User CreateUser(Guid userId, DateTime utcNow) =>
        new()
        {
            Id = userId,
            Email = $"summary-{userId:N}@example.com",
            NormalizedEmail = $"SUMMARY-{userId:N}@example.com",
            UserName = $"summary-{userId:N}",
            DisplayName = "Summary User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    private static void AddMovie(ApplicationDbContext context, Guid movieId, int tmdbId, DateTime utcNow) =>
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = tmdbId,
            Title = "Movie",
            VoteAverage = 7,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

    private static WatchedMovie WatchedMovie(Guid userId, Guid movieId, DateTime watchedAt, DateTime utcNow) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            WatchedAt = watchedAt,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    private static WatchedEpisode WatchedEpisode(Guid userId, Guid episodeId, DateTime watchedAt, DateTime utcNow) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EpisodeId = episodeId,
            WatchedAt = watchedAt,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_summary_sql_tests"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
