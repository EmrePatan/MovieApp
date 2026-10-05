using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Insights;

public sealed class InsightsV3AggregateSqlIntegrationTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly DateTime UtcNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SqlAggregatesMatchPreviousInMemorySemanticsWithoutLoadingEveryWatch()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var seeded = await SeedHistoryAsync(context);
        var collector = new CommandTextCollector();
        var repository = CreateInsightsRepository(collector);
        var (raw, _) = await repository.GetV3RawDataAsync(seeded.UserId, Istanbul, 2026);
        var result = InsightsV3Builder.Build(raw, Istanbul, 2026, UtcNow);

        var expectedTaste = InsightsTasteBuilder.Build(AnalyticsRaw(seeded.AllTimeMovies, seeded.AllTimeShows));
        Assert.Equal(
            expectedTaste.Genres.Select(genre => (genre.GenreId, genre.Name, genre.Weight, genre.SharePercent)),
            result.YourTaste.Genres.Select(genre => (genre.GenreId, genre.Name, genre.Weight, genre.SharePercent)));
        Assert.Equal(
            result.YourTaste.Genres.Select(genre => (genre.GenreId, genre.SharePercent)),
            result.MovieDna.TopGenres.Select(genre => (genre.GenreId, genre.SharePercent)));

        var expectedDna = InsightsMovieDnaBuilder.Build(
            new InsightsSummaryRawData(
                seeded.MemberSince,
                seeded.AllTimeMovies.Count,
                seeded.Episodes,
                seeded.AllTimeShows.Count,
                0,
                [],
                seeded.AllTimeMovies,
                seeded.AllTimeShows),
            UtcNow);
        Assert.Equal(
            expectedDna.Select(label => (label.Code, label.Category, label.Label)),
            result.MovieDna.Labels.Select(label => (label.Code, label.Category, label.Label)));

        var expectedEras = InsightsErasBuilder.Build(AnalyticsRaw(seeded.AllTimeMovies, seeded.AllTimeShows));
        Assert.Equal(expectedEras.UnknownCount, result.YourEra.UnknownCount);
        Assert.Equal(
            expectedEras.Buckets.Select(bucket => (bucket.Bucket, bucket.Count, bucket.Percent)),
            result.YourEra.Decades.Select(bucket => (bucket.Bucket, bucket.Count, bucket.Percent)));
        Assert.Equal(
            expectedEras.Buckets
                .Where(bucket => bucket.Count > 0)
                .OrderByDescending(bucket => bucket.Count)
                .ThenBy(bucket => bucket.Bucket, StringComparer.Ordinal)
                .Select(bucket => bucket.Bucket)
                .First(),
            result.YourEra.FavoriteDecade);

        var expectedRising = InsightsV3TasteBuilder.TryBuildRisingGenre(
            seeded.CurrentYearMovies,
            seeded.CurrentYearEpisodes,
            seeded.PreviousYearMovies,
            seeded.PreviousYearEpisodes);
        Assert.Equal(expectedRising, result.YourTaste.RisingGenre);

        var expectedYear = ExpectedYearActivity(seeded.Watches);
        Assert.Equal(expectedYear.ActiveDays, result.YourYear.ActiveDays);
        Assert.Equal(expectedYear.YearMinutes, result.TimeInStories.YearMinutes);
        Assert.Equal(expectedYear.FavoriteWeekday, result.YourYear.FavoriteWeekday);
        Assert.Equal(expectedYear.PeakMonth, result.YourYear.PeakMonth?.Month);
        Assert.Equal(expectedYear.PeakTotal, result.YourYear.PeakMonth?.Total);
        foreach (var month in result.YourYear.Months)
        {
            var expected = expectedYear.Months[month.Month];
            Assert.Equal(expected.Movies, month.Movies);
            Assert.Equal(expected.Episodes, month.Episodes);
        }

        var expectedGenres = InsightsMilestonesBuilder.CountDistinctGenres(
            AnalyticsRaw(seeded.AllTimeMovies, seeded.AllTimeShows));
        Assert.Equal(
            expectedGenres,
            result.Achievements.Single(item => item.Id == "genres-5").CurrentValue);

        var showOnlySciFi = raw.AllTimeGenreContributions.Single(row =>
            row.GenreId == seeded.SciFiId && row.GenresOnTitle == 2);
        Assert.Equal(1, showOnlySciFi.TitleCount);
        var currentYearSciFi = raw.CurrentYearGenreContributions.Single(row => row.GenreId == seeded.SciFiId);
        Assert.Equal(2, currentYearSciFi.GenresOnTitle);
        Assert.Equal(2, currentYearSciFi.TitleCount);

        var commands = collector.Commands.ToList();
        Assert.NotEmpty(commands);
        Assert.Contains(commands, sql =>
            sql.Contains("watched_movies", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
        Assert.All(commands, sql => Assert.True(WatchQueryIsAggregated(sql), sql));
    }

    private static bool WatchQueryIsAggregated(string sql)
    {
        var text = sql.ToUpperInvariant();
        var readsWatches = text.Contains("WATCHED_MOVIES", StringComparison.Ordinal)
            || text.Contains("WATCHED_EPISODES", StringComparison.Ordinal);
        if (!readsWatches)
        {
            return true;
        }

        return text.Contains("COUNT(", StringComparison.Ordinal)
            || text.Contains("SUM(", StringComparison.Ordinal)
            || text.Contains("GROUP BY", StringComparison.Ordinal)
            || text.Contains("ROW_NUMBER(", StringComparison.Ordinal)
            || text.Contains("LIMIT", StringComparison.Ordinal);
    }

    private static ExpectedYear ExpectedYearActivity(IReadOnlyList<WatchSeed> watches)
    {
        var (start, end) = InsightsV3TimeRangeHelper.GetCalendarYearUtcBounds(2026, Istanbul);
        var dayCounts = new Dictionary<DateOnly, (int Movies, int Episodes)>();
        var minutes = 0;

        foreach (var watch in watches)
        {
            if (watch.WatchedAt < start || watch.WatchedAt >= end)
            {
                continue;
            }

            var date = InsightsV3TimeRangeHelper.ToLocalDate(watch.WatchedAt, Istanbul);
            if (date.Year != 2026)
            {
                continue;
            }

            if (!dayCounts.TryGetValue(date, out var counts))
            {
                counts = (0, 0);
            }

            if (watch.IsMovie)
            {
                counts.Movies++;
            }
            else
            {
                counts.Episodes++;
            }

            dayCounts[date] = counts;
            if (watch.RuntimeMinutes is > 0)
            {
                minutes += watch.RuntimeMinutes.Value;
            }
        }

        var months = Enumerable.Range(1, 12)
            .ToDictionary(month => month, month =>
            {
                var matches = dayCounts.Where(pair => pair.Key.Month == month).ToList();
                return (
                    Movies: matches.Sum(pair => pair.Value.Movies),
                    Episodes: matches.Sum(pair => pair.Value.Episodes));
            });
        var peak = months
            .Select(pair => (Month: pair.Key, Total: pair.Value.Movies + pair.Value.Episodes))
            .Where(pair => pair.Total > 0)
            .OrderByDescending(pair => pair.Total)
            .ThenByDescending(pair => pair.Month)
            .First();
        var weekdayTotals = dayCounts
            .GroupBy(pair => pair.Key.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value.Movies + pair.Value.Episodes));

        return new ExpectedYear(
            months,
            dayCounts.Count,
            minutes,
            peak.Month,
            peak.Total,
            InsightsV3YourYearBuilder.CalculateFavoriteWeekday(weekdayTotals));
    }

    private static InsightsAnalyticsRawData AnalyticsRaw(
        IReadOnlyList<InsightsDnaTitleData> movies,
        IReadOnlyList<InsightsDnaTitleData> shows) =>
        new(
            UtcNow,
            movies.Count,
            0,
            shows.Count,
            0,
            [],
            movies,
            shows,
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static async Task<SeededHistory> SeedHistoryAsync(ApplicationDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userId = Guid.NewGuid();
        var memberSince = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var utcNow = DateTime.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"agg-{suffix}@example.com",
            NormalizedEmail = $"AGG-{suffix}@EXAMPLE.COM",
            UserName = $"agg-{suffix}",
            DisplayName = "Aggregate User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = memberSince,
            UpdatedAt = utcNow,
        });

        var actionId = Guid.NewGuid();
        var dramaId = Guid.NewGuid();
        var comedyId = Guid.NewGuid();
        var sciFiId = Guid.NewGuid();
        context.Genres.AddRange(
            Genre(actionId, $"Action-{suffix}", utcNow),
            Genre(dramaId, $"Drama-{suffix}", utcNow),
            Genre(comedyId, $"Comedy-{suffix}", utcNow),
            Genre(sciFiId, $"SciFi-{suffix}", utcNow));

        var action = new InsightsDnaGenreData(actionId, $"Action-{suffix}");
        var drama = new InsightsDnaGenreData(dramaId, $"Drama-{suffix}");
        var comedy = new InsightsDnaGenreData(comedyId, $"Comedy-{suffix}");
        var sciFi = new InsightsDnaGenreData(sciFiId, $"SciFi-{suffix}");

        var movies = new (string Title, DateOnly? ReleaseDate, int? Runtime, DateTime WatchedAt, InsightsDnaGenreData[] Genres)[]
        {
            ("Dual", new DateOnly(2024, 1, 1), 120, new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc), [action, drama]),
            ("Action", new DateOnly(2015, 5, 1), 90, new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc), [action]),
            ("Unknown", null, null, new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc), []),
            ("Comedy", new DateOnly(1995, 1, 1), 0, new DateTime(2025, 12, 31, 21, 30, 0, DateTimeKind.Utc), [comedy]),
            ("SciFi", new DateOnly(2026, 2, 1), 100, new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc), [sciFi]),
            ("Older", new DateOnly(1988, 1, 1), 110, new DateTime(2025, 12, 31, 20, 0, 0, DateTimeKind.Utc), [drama]),
        };

        var watches = new List<WatchSeed>();
        var tmdbBase = Random.Shared.Next(10_000_000, 80_000_000);
        for (var index = 0; index < movies.Length; index++)
        {
            var movie = movies[index];
            var movieId = Guid.NewGuid();
            context.Movies.Add(new Movie
            {
                Id = movieId,
                TmdbId = tmdbBase + index,
                Title = movie.Title,
                ReleaseDate = movie.ReleaseDate,
                RuntimeMinutes = movie.Runtime,
                VoteAverage = 7,
                VoteCount = 1,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            foreach (var genre in movie.Genres)
            {
                context.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genre.GenreId });
            }

            context.WatchedMovies.Add(new WatchedMovie
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieId,
                WatchedAt = movie.WatchedAt,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            watches.Add(new WatchSeed(movie.WatchedAt, movie.Runtime, true));
        }

        var showA = Guid.NewGuid();
        var showB = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showA,
            TmdbId = tmdbBase + 100,
            Title = "Show A",
            FirstAirDate = new DateOnly(2018, 1, 1),
            Status = TvShowStatus.Ended,
            VoteAverage = 8,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.TvShows.Add(new TvShow
        {
            Id = showB,
            TmdbId = tmdbBase + 101,
            Title = "Show B",
            Status = TvShowStatus.Ended,
            VoteAverage = 6,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.TvShowGenres.Add(new TvShowGenre { TvShowId = showA, GenreId = dramaId });
        context.TvShowGenres.Add(new TvShowGenre { TvShowId = showA, GenreId = sciFiId });

        var seasonA = Guid.NewGuid();
        var seasonB = Guid.NewGuid();
        context.Seasons.Add(Season(seasonA, showA, utcNow));
        context.Seasons.Add(Season(seasonB, showB, utcNow));

        var episodes = new (Guid SeasonId, int? Runtime, DateTime WatchedAt)[]
        {
            (seasonA, 40, new DateTime(2026, 3, 2, 10, 0, 0, DateTimeKind.Utc)),
            (seasonA, 45, new DateTime(2026, 3, 2, 18, 0, 0, DateTimeKind.Utc)),
            (seasonA, null, new DateTime(2025, 2, 2, 12, 0, 0, DateTimeKind.Utc)),
            (seasonB, 30, new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc)),
        };
        for (var index = 0; index < episodes.Length; index++)
        {
            var episode = episodes[index];
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = episode.SeasonId,
                EpisodeNumber = index + 1,
                Name = $"Episode {index + 1}",
                RuntimeMinutes = episode.Runtime,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            context.WatchedEpisodes.Add(new WatchedEpisode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EpisodeId = episodeId,
                WatchedAt = episode.WatchedAt,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            watches.Add(new WatchSeed(episode.WatchedAt, episode.Runtime, false));
        }

        await context.SaveChangesAsync();

        var (currentStart, currentEnd) = InsightsV3TimeRangeHelper.GetCalendarYearUtcBounds(2026, Istanbul);
        var (previousStart, previousEnd) = InsightsV3TimeRangeHelper.GetCalendarYearUtcBounds(2025, Istanbul);
        bool InRange(DateTime watchedAt, DateTime start, DateTime end) =>
            watchedAt >= start && watchedAt < end;

        var allTimeMovies = movies
            .Select(movie => new InsightsDnaTitleData(movie.ReleaseDate?.Year, movie.Genres))
            .ToList();
        var showATitle = new InsightsDnaTitleData(2018, [drama, sciFi]);
        var showBTitle = new InsightsDnaTitleData(null, []);

        return new SeededHistory(
            userId,
            memberSince,
            sciFiId,
            allTimeMovies,
            [showATitle, showBTitle],
            movies.Where(movie => InRange(movie.WatchedAt, currentStart, currentEnd))
                .Select(movie => new InsightsDnaTitleData(movie.ReleaseDate?.Year, movie.Genres))
                .ToList(),
            movies.Where(movie => InRange(movie.WatchedAt, previousStart, previousEnd))
                .Select(movie => new InsightsDnaTitleData(movie.ReleaseDate?.Year, movie.Genres))
                .ToList(),
            episodes.Where(episode => episode.SeasonId == seasonA && InRange(episode.WatchedAt, currentStart, currentEnd))
                .Select(_ => showATitle)
                .ToList(),
            episodes.Where(episode => episode.SeasonId == seasonA && InRange(episode.WatchedAt, previousStart, previousEnd))
                .Select(_ => showATitle)
                .ToList(),
            episodes.Length,
            watches);
    }

    private static Genre Genre(Guid id, string name, DateTime createdAt) =>
        new()
        {
            Id = id,
            Name = name,
            CreatedAt = createdAt,
        };

    private static Season Season(Guid id, Guid tvShowId, DateTime utcNow) =>
        new()
        {
            Id = id,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_aggregate_sql_tests"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static InsightsRepository CreateInsightsRepository(CommandTextCollector collector)
    {
        var connectionString = IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_aggregate_sql_tests");
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString).AddInterceptors(collector));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new InsightsRepository(scopeFactory);
    }

    private sealed record WatchSeed(DateTime WatchedAt, int? RuntimeMinutes, bool IsMovie);

    private sealed record ExpectedYear(
        IReadOnlyDictionary<int, (int Movies, int Episodes)> Months,
        int ActiveDays,
        int YearMinutes,
        int PeakMonth,
        int PeakTotal,
        DayOfWeek? FavoriteWeekday);

    private sealed record SeededHistory(
        Guid UserId,
        DateTime MemberSince,
        Guid SciFiId,
        IReadOnlyList<InsightsDnaTitleData> AllTimeMovies,
        IReadOnlyList<InsightsDnaTitleData> AllTimeShows,
        IReadOnlyList<InsightsDnaTitleData> CurrentYearMovies,
        IReadOnlyList<InsightsDnaTitleData> PreviousYearMovies,
        IReadOnlyList<InsightsDnaTitleData> CurrentYearEpisodes,
        IReadOnlyList<InsightsDnaTitleData> PreviousYearEpisodes,
        int Episodes,
        IReadOnlyList<WatchSeed> Watches);

    private sealed class CommandTextCollector : DbCommandInterceptor
    {
        private readonly ConcurrentBag<string> commands = [];

        public IReadOnlyCollection<string> Commands => commands;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
