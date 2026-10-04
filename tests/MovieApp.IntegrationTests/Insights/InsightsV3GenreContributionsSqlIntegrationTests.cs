using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Insights;

public sealed class InsightsV3GenreContributionsSqlIntegrationTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private const int InsightsYear = 2026;

    [Fact]
    public async Task GetGenreContributionsAsyncMatchesLegacySqlAcrossScenarios()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var bounds = CreateYearBounds();

        await AssertParityAsync(context, await SeedNoActivityAsync(context), bounds);
        await AssertParityAsync(context, await SeedMoviesOnlyAsync(context), bounds);
        await AssertParityAsync(context, await SeedTvOnlySingleShowAsync(context, episodeCount: 5), bounds);
        await AssertParityAsync(context, await SeedSameShowMultipleSeasonsAsync(context), bounds);
        await AssertParityAsync(context, await SeedMultipleShowsAsync(context), bounds);
        await AssertParityAsync(context, await SeedMultiGenreMovieAsync(context), bounds);
        await AssertParityAsync(context, await SeedMixedMovieAndTvAsync(context), bounds);
        await AssertParityAsync(context, await SeedCurrentYearOnlyAsync(context), bounds);
        await AssertParityAsync(context, await SeedPreviousYearOnlyAsync(context), bounds);
        await AssertParityAsync(context, await SeedMovieWithoutGenresAsync(context), bounds);
        await AssertParityAsync(context, await SeedYearBoundaryAsync(context), bounds);
    }

    [Fact]
    public async Task GetGenreContributionsAsyncPreservesDistinctShowVersusEpisodeSemantics()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var bounds = CreateYearBounds();
        var userId = await SeedTvOnlySingleShowAsync(context, episodeCount: 8);
        var optimized = await InsightsV3AggregateQueries.GetGenreContributionsAsync(
            context,
            userId,
            bounds.PreviousStart,
            bounds.PreviousEnd,
            bounds.CurrentStart,
            bounds.CurrentEnd,
            CancellationToken.None);

        var drama = optimized.AllTime.Single(row => row.Name.StartsWith("Drama-", StringComparison.Ordinal));
        Assert.Equal(1, drama.TitleCount);
        Assert.Equal(1, drama.GenresOnTitle);

        var currentDrama = optimized.CurrentYear.Single(row => row.Name.StartsWith("Drama-", StringComparison.Ordinal));
        Assert.Equal(8, currentDrama.TitleCount);
    }

    private static YearBounds CreateYearBounds()
    {
        var (currentStart, currentEnd) = InsightsV3TimeRangeHelper.GetCalendarYearUtcBounds(InsightsYear, Istanbul);
        var (previousStart, previousEnd) = InsightsV3TimeRangeHelper.GetCalendarYearUtcBounds(InsightsYear - 1, Istanbul);
        return new YearBounds(previousStart, previousEnd, currentStart, currentEnd);
    }

    private static async Task AssertParityAsync(
        ApplicationDbContext context,
        Guid userId,
        YearBounds bounds)
    {
        var optimized = await InsightsV3AggregateQueries.GetGenreContributionsAsync(
            context,
            userId,
            bounds.PreviousStart,
            bounds.PreviousEnd,
            bounds.CurrentStart,
            bounds.CurrentEnd,
            CancellationToken.None);

        var legacy = await GetGenreContributionsLegacyAsync(
            context,
            userId,
            bounds.PreviousStart,
            bounds.PreviousEnd,
            bounds.CurrentStart,
            bounds.CurrentEnd,
            CancellationToken.None);

        AssertContributionSetsEqual(legacy, optimized);
    }

    private static void AssertContributionSetsEqual(
        InsightsV3AggregateQueries.GenreContributionSets legacy,
        InsightsV3AggregateQueries.GenreContributionSets optimized)
    {
        AssertContributionsEqual(legacy.AllTime, optimized.AllTime);
        AssertContributionsEqual(legacy.CurrentYear, optimized.CurrentYear);
        AssertContributionsEqual(legacy.PreviousYear, optimized.PreviousYear);
    }

    private static void AssertContributionsEqual(
        IReadOnlyList<InsightsV3GenreContribution> legacy,
        IReadOnlyList<InsightsV3GenreContribution> optimized)
    {
        var order = (InsightsV3GenreContribution row) =>
            (row.GenreId, row.Name, row.GenresOnTitle, row.TitleCount);

        Assert.Equal(
            legacy.OrderBy(order).ToList(),
            optimized.OrderBy(order).ToList());
    }

    private static async Task<InsightsV3AggregateQueries.GenreContributionSets> GetGenreContributionsLegacyAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        DateTime previousYearStartUtc,
        DateTime previousYearEndUtc,
        DateTime currentYearStartUtc,
        DateTime currentYearEndUtc,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<GenreContributionSqlRow>($"""
                SELECT
                    weighted.scope AS "Scope",
                    weighted.genre_id AS "GenreId",
                    weighted.genre_name AS "Name",
                    weighted.genre_count AS "GenresOnTitle",
                    COUNT(*)::integer AS "TitleCount"
                FROM (
                    SELECT
                        'all_time'::text AS scope,
                        mg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY wm."Id")::integer AS genre_count
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                    INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                    WHERE wm."UserId" = {userId}
                    UNION ALL
                    SELECT
                        'all_time'::text AS scope,
                        tg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY shows.show_id)::integer AS genre_count
                    FROM (
                        SELECT DISTINCT s."TvShowId" AS show_id
                        FROM watched_episodes AS we
                        INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                        INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                        WHERE we."UserId" = {userId}
                    ) AS shows
                    INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = shows.show_id
                    INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                    UNION ALL
                    SELECT
                        'current_year'::text AS scope,
                        mg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY wm."Id")::integer AS genre_count
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                    INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                    WHERE wm."UserId" = {userId}
                      AND wm."WatchedAt" >= {currentYearStartUtc}
                      AND wm."WatchedAt" < {currentYearEndUtc}
                    UNION ALL
                    SELECT
                        'previous_year'::text AS scope,
                        mg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY wm."Id")::integer AS genre_count
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                    INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                    WHERE wm."UserId" = {userId}
                      AND wm."WatchedAt" >= {previousYearStartUtc}
                      AND wm."WatchedAt" < {previousYearEndUtc}
                    UNION ALL
                    SELECT
                        'current_year'::text AS scope,
                        tg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY we."Id")::integer AS genre_count
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                    INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = s."TvShowId"
                    INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                    WHERE we."UserId" = {userId}
                      AND we."WatchedAt" >= {currentYearStartUtc}
                      AND we."WatchedAt" < {currentYearEndUtc}
                    UNION ALL
                    SELECT
                        'previous_year'::text AS scope,
                        tg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY we."Id")::integer AS genre_count
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                    INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = s."TvShowId"
                    INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                    WHERE we."UserId" = {userId}
                      AND we."WatchedAt" >= {previousYearStartUtc}
                      AND we."WatchedAt" < {previousYearEndUtc}
                ) AS weighted
                GROUP BY weighted.scope, weighted.genre_id, weighted.genre_name, weighted.genre_count
                """)
            .ToListAsync(cancellationToken);

        return new InsightsV3AggregateQueries.GenreContributionSets(
            ToContributions(rows, "all_time"),
            ToContributions(rows, "current_year"),
            ToContributions(rows, "previous_year"));
    }

    private static List<InsightsV3GenreContribution> ToContributions(
        IReadOnlyList<GenreContributionSqlRow> rows,
        string scope) =>
        rows
            .Where(row => row.Scope == scope)
            .Select(row => new InsightsV3GenreContribution(
                row.GenreId,
                row.Name,
                row.GenresOnTitle,
                row.TitleCount))
            .ToList();

    private sealed class GenreContributionSqlRow
    {
        public string Scope { get; init; } = string.Empty;

        public Guid GenreId { get; init; }

        public string Name { get; init; } = string.Empty;

        public int GenresOnTitle { get; init; }

        public int TitleCount { get; init; }
    }

    private sealed record YearBounds(
        DateTime PreviousStart,
        DateTime PreviousEnd,
        DateTime CurrentStart,
        DateTime CurrentEnd);

    private static async Task<Guid> SeedNoActivityAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        context.Users.Add(CreateUser(userId));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMoviesOnlyAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));

        var dramaId = Guid.NewGuid();
        context.Genres.Add(Genre(dramaId, $"Drama-{suffix}", utcNow));
        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, utcNow);
        context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = dramaId });
        context.WatchedMovies.Add(WatchedMovie(
            userId,
            movieId,
            new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            utcNow));

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedTvOnlySingleShowAsync(ApplicationDbContext context, int episodeCount)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));

        var dramaId = Guid.NewGuid();
        context.Genres.Add(Genre(dramaId, $"Drama-{suffix}", utcNow));
        var (showId, seasonId) = AddShowWithSeason(context, utcNow, suffix);

        context.TvShowGenres.Add(new TvShowGenre { TvShowId = showId, GenreId = dramaId });

        for (var index = 0; index < episodeCount; index++)
        {
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = index + 1,
                Name = $"E{index + 1}",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(WatchedEpisode(
                userId,
                episodeId,
                new DateTime(2026, 3, index + 1, 12, 0, 0, DateTimeKind.Utc),
                utcNow));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedSameShowMultipleSeasonsAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));

        var comedyId = Guid.NewGuid();
        context.Genres.Add(Genre(comedyId, $"Comedy-{suffix}", utcNow));
        var showId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            TmdbId = Random.Shared.Next(10_000_000, 90_000_000),
            Title = "Multi",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.TvShowGenres.Add(new TvShowGenre { TvShowId = showId, GenreId = comedyId });

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
                Name = $"S{seasonNumber}",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(WatchedEpisode(
                userId,
                episodeId,
                new DateTime(2026, 4, seasonNumber, 12, 0, 0, DateTimeKind.Utc),
                utcNow));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMultipleShowsAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Users.Add(CreateUser(userId));

        for (var index = 0; index < 3; index++)
        {
            var suffix = $"{index}-{Guid.NewGuid():N}"[..8];
            var genreId = Guid.NewGuid();
            context.Genres.Add(Genre(genreId, $"Genre-{suffix}", utcNow));
            var (showId, seasonId) = AddShowWithSeason(context, utcNow, suffix);
            context.TvShowGenres.Add(new TvShowGenre { TvShowId = showId, GenreId = genreId });
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = 1,
                Name = "Pilot",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(WatchedEpisode(
                userId,
                episodeId,
                new DateTime(2026, 5, index + 1, 12, 0, 0, DateTimeKind.Utc),
                utcNow));
        }

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMultiGenreMovieAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));

        var dramaId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        context.Genres.AddRange(
            Genre(dramaId, $"Drama-{suffix}", utcNow),
            Genre(actionId, $"Action-{suffix}", utcNow));

        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, utcNow);
        context.MovieGenres.AddRange(
            new MovieGenre { MovieId = movieId, GenreId = dramaId },
            new MovieGenre { MovieId = movieId, GenreId = actionId });
        context.WatchedMovies.Add(WatchedMovie(
            userId,
            movieId,
            new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            utcNow));

        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMixedMovieAndTvAsync(ApplicationDbContext context)
    {
        var userId = await SeedMultiGenreMovieAsync(context);
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var sciFiId = Guid.NewGuid();
        context.Genres.Add(Genre(sciFiId, $"SciFi-{suffix}", utcNow));
        var (showId, seasonId) = AddShowWithSeason(context, utcNow, suffix);
        context.TvShowGenres.Add(new TvShowGenre { TvShowId = showId, GenreId = sciFiId });
        var episodeId = Guid.NewGuid();
        context.Episodes.Add(new Episode
        {
            Id = episodeId,
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Pilot",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.WatchedEpisodes.Add(WatchedEpisode(
            userId,
            episodeId,
            new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            utcNow));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedCurrentYearOnlyAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));
        var genreId = Guid.NewGuid();
        context.Genres.Add(Genre(genreId, $"Drama-{suffix}", utcNow));
        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, utcNow);
        context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
        context.WatchedMovies.Add(WatchedMovie(
            userId,
            movieId,
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            utcNow));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedPreviousYearOnlyAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));
        var genreId = Guid.NewGuid();
        context.Genres.Add(Genre(genreId, $"Drama-{suffix}", utcNow));
        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, utcNow);
        context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
        context.WatchedMovies.Add(WatchedMovie(
            userId,
            movieId,
            new DateTime(2025, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            utcNow));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedMovieWithoutGenresAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Users.Add(CreateUser(userId));
        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, utcNow);
        context.WatchedMovies.Add(WatchedMovie(
            userId,
            movieId,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            utcNow));
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedYearBoundaryAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..6];
        context.Users.Add(CreateUser(userId));
        var genreId = Guid.NewGuid();
        context.Genres.Add(Genre(genreId, $"Drama-{suffix}", utcNow));
        var movieId = Guid.NewGuid();
        AddMovie(context, movieId, utcNow);
        context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });

        var (currentStart, _) = InsightsV3TimeRangeHelper.GetCalendarYearUtcBounds(InsightsYear, Istanbul);
        context.WatchedMovies.Add(WatchedMovie(userId, movieId, currentStart.AddMinutes(-1), utcNow));

        var inYearMovieId = Guid.NewGuid();
        AddMovie(context, inYearMovieId, utcNow);
        context.MovieGenres.Add(new MovieGenre { MovieId = inYearMovieId, GenreId = genreId });
        context.WatchedMovies.Add(WatchedMovie(userId, inYearMovieId, currentStart.AddHours(1), utcNow));

        await context.SaveChangesAsync();
        return userId;
    }

    private static User CreateUser(Guid userId)
    {
        var utcNow = DateTime.UtcNow;
        return new User
        {
            Id = userId,
            Email = $"genre-{userId:N}@example.com",
            NormalizedEmail = $"GENRE-{userId:N}@example.com",
            UserName = $"genre-{userId:N}",
            DisplayName = "Genre User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    private static Genre Genre(Guid id, string name, DateTime utcNow) =>
        new() { Id = id, Name = name, CreatedAt = utcNow };

    private static void AddMovie(ApplicationDbContext context, Guid movieId, DateTime utcNow) =>
        context.Movies.Add(new Movie
        {
            Id = movieId,
            TmdbId = Random.Shared.Next(10_000_000, 90_000_000),
            Title = "Movie",
            VoteAverage = 7,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

    private static (Guid ShowId, Guid SeasonId) AddShowWithSeason(
        ApplicationDbContext context,
        DateTime utcNow,
        string suffix)
    {
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            TmdbId = Random.Shared.Next(10_000_000, 90_000_000),
            Title = $"Show-{suffix}",
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
        return (showId, seasonId);
    }

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
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_genre_sql_tests"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
