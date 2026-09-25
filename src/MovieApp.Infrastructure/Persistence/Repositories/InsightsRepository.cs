using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class InsightsRepository(
    ApplicationDbContext dbContext,
    IServiceScopeFactory scopeFactory) : IInsightsRepository
{
    private sealed record SummaryCountsRow(
        DateTime MemberSince,
        int MoviesWatched,
        int EpisodesWatched,
        int ShowsStarted,
        int RatingsCount);

    private sealed record V3SummaryRow(
        DateTime MemberSince,
        int MoviesWatched,
        int EpisodesWatched,
        int ShowsStarted,
        int RatingsCount,
        int DistinctMovieCount,
        int DistinctSeriesCount);

    private sealed record V3RuntimeTotalsRow(
        int MovieTotalMinutes,
        int MovieKnownCount,
        int EpisodeTotalMinutes,
        int EpisodeKnownCount);

    public async Task<(InsightsSummaryRawData Raw, InsightsSummaryQueryMetrics Metrics)> GetSummaryRawDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var metrics = new InsightsSummaryQueryMetrics();
        var dbStopwatch = Stopwatch.StartNew();

        var counts = await ExecuteTimedQueryAsync(
            metrics,
            () => GetSummaryCountsAsync(userId, cancellationToken));

        var ratingScoreCounts = await ExecuteTimedQueryAsync(
            metrics,
            () => GetRatingScoreCountsAsync(dbContext, userId, cancellationToken));

        var movieTitles = await ExecuteTimedQueryAsync(
            metrics,
            () => GetMovieDnaTitlesAsync(userId, cancellationToken));

        var tvShowTitles = await ExecuteTimedQueryAsync(
            metrics,
            () => GetTvShowDnaTitlesAsync(userId, cancellationToken));

        dbStopwatch.Stop();
        metrics.DbTotalMs = dbStopwatch.ElapsedMilliseconds;

        var raw = new InsightsSummaryRawData(
            counts.MemberSince,
            counts.MoviesWatched,
            counts.EpisodesWatched,
            counts.ShowsStarted,
            counts.RatingsCount,
            ratingScoreCounts,
            movieTitles,
            tvShowTitles);

        return (raw, metrics);
    }

    public async Task<(InsightsAnalyticsRawData Raw, InsightsAnalyticsQueryMetrics Metrics)> GetAnalyticsRawDataAsync(
        Guid userId,
        DateTime activityUtcStart,
        CancellationToken cancellationToken = default)
    {
        var metrics = new InsightsAnalyticsQueryMetrics();
        var dbStopwatch = Stopwatch.StartNew();

        var counts = await ExecuteTimedQueryAsync(
            metrics,
            () => GetSummaryCountsAsync(userId, cancellationToken));

        var activityStopwatch = Stopwatch.StartNew();
        var movieActivityEvents = await ExecuteTimedQueryAsync(
            metrics,
            () => GetMovieActivityEventsAsync(userId, activityUtcStart, cancellationToken));
        var episodeActivityEvents = await ExecuteTimedQueryAsync(
            metrics,
            () => GetEpisodeActivityEventsAsync(userId, activityUtcStart, cancellationToken));
        var activityEvents = movieActivityEvents.Concat(episodeActivityEvents).ToList();
        activityStopwatch.Stop();
        metrics.ActivityMs = activityStopwatch.ElapsedMilliseconds;

        var tasteErasStopwatch = Stopwatch.StartNew();
        var movieTitles = await ExecuteTimedQueryAsync(
            metrics,
            () => GetMovieDnaTitlesAsync(userId, cancellationToken));
        var tvShowTitles = await ExecuteTimedQueryAsync(
            metrics,
            () => GetTvShowDnaTitlesAsync(userId, cancellationToken));
        tasteErasStopwatch.Stop();
        metrics.TasteErasMs = tasteErasStopwatch.ElapsedMilliseconds;

        var runtimeStopwatch = Stopwatch.StartNew();
        var movieRuntime = await ExecuteTimedQueryAsync(
            metrics,
            () => GetMovieRuntimeAggregateAsync(userId, cancellationToken));
        var episodeRuntime = await ExecuteTimedQueryAsync(
            metrics,
            () => GetEpisodeRuntimeAggregateAsync(userId, cancellationToken));
        runtimeStopwatch.Stop();
        metrics.RuntimeMs = runtimeStopwatch.ElapsedMilliseconds;

        var ratingsStopwatch = Stopwatch.StartNew();
        var ratingScoreCounts = await ExecuteTimedQueryAsync(
            metrics,
            () => GetRatingScoreCountsAsync(dbContext, userId, cancellationToken));
        ratingsStopwatch.Stop();
        metrics.RatingsMs = ratingsStopwatch.ElapsedMilliseconds;

        var milestonesStopwatch = Stopwatch.StartNew();
        var showCompletions = await ExecuteTimedQueryAsync(
            metrics,
            () => GetShowCompletionProjectionAsync(userId, cancellationToken));
        var firstMovieWatchedAt = counts.MoviesWatched >= 1
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthMovieWatchedAtAsync(userId, 1, cancellationToken))
            : null;
        var tenthMovieWatchedAt = counts.MoviesWatched >= 10
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthMovieWatchedAtAsync(userId, 10, cancellationToken))
            : null;
        var fiftiethMovieWatchedAt = counts.MoviesWatched >= 50
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthMovieWatchedAtAsync(userId, 50, cancellationToken))
            : null;
        var hundredthEpisodeWatchedAt = counts.EpisodesWatched >= 100
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthEpisodeWatchedAtAsync(userId, 100, cancellationToken))
            : null;
        var fiveHundredthEpisodeWatchedAt = counts.EpisodesWatched >= 500
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthEpisodeWatchedAtAsync(userId, 500, cancellationToken))
            : null;
        var tenthRatingAt = counts.RatingsCount >= 10
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthRatingAtAsync(userId, 10, cancellationToken))
            : null;
        var twentyFifthRatingAt = counts.RatingsCount >= 25
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthRatingAtAsync(userId, 25, cancellationToken))
            : null;
        var fiftiethRatingAt = counts.RatingsCount >= 50
            ? await ExecuteTimedQueryAsync(
                metrics,
                () => GetNthRatingAtAsync(userId, 50, cancellationToken))
            : null;
        milestonesStopwatch.Stop();
        metrics.MilestonesMs = milestonesStopwatch.ElapsedMilliseconds;

        dbStopwatch.Stop();
        metrics.DbTotalMs = dbStopwatch.ElapsedMilliseconds;

        var raw = new InsightsAnalyticsRawData(
            counts.MemberSince,
            counts.MoviesWatched,
            counts.EpisodesWatched,
            counts.ShowsStarted,
            counts.RatingsCount,
            activityEvents,
            movieTitles,
            tvShowTitles,
            movieRuntime.TotalMinutes,
            movieRuntime.KnownCount,
            episodeRuntime.TotalMinutes,
            episodeRuntime.KnownCount,
            ratingScoreCounts,
            showCompletions,
            firstMovieWatchedAt,
            tenthMovieWatchedAt,
            fiftiethMovieWatchedAt,
            hundredthEpisodeWatchedAt,
            fiveHundredthEpisodeWatchedAt,
            tenthRatingAt,
            twentyFifthRatingAt,
            fiftiethRatingAt);

        return (raw, metrics);
    }

    private sealed record RuntimeAggregateRow(int TotalMinutes, int KnownCount);

    private async Task<IReadOnlyList<InsightsActivityEventData>> GetMovieActivityEventsAsync(
        Guid userId,
        DateTime activityUtcStart,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId && watchedMovie.WatchedAt >= activityUtcStart)
            .Select(watchedMovie => new InsightsActivityEventData(
                watchedMovie.WatchedAt,
                true,
                watchedMovie.Movie!.RuntimeMinutes))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InsightsActivityEventData>> GetEpisodeActivityEventsAsync(
        Guid userId,
        DateTime activityUtcStart,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId && watchedEpisode.WatchedAt >= activityUtcStart)
            .Select(watchedEpisode => new InsightsActivityEventData(
                watchedEpisode.WatchedAt,
                false,
                watchedEpisode.Episode!.RuntimeMinutes))
            .ToListAsync(cancellationToken);
    }

    private async Task<RuntimeAggregateRow> GetMovieRuntimeAggregateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var totals = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId && watchedMovie.Movie!.RuntimeMinutes > 0)
            .GroupBy(_ => 1)
            .Select(group => new RuntimeAggregateRow(
                group.Sum(watchedMovie => watchedMovie.Movie!.RuntimeMinutes ?? 0),
                group.Count()))
            .SingleRowOrDefaultAsync(cancellationToken);

        return totals ?? new RuntimeAggregateRow(0, 0);
    }

    private async Task<RuntimeAggregateRow> GetEpisodeRuntimeAggregateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var totals = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId && watchedEpisode.Episode!.RuntimeMinutes > 0)
            .GroupBy(_ => 1)
            .Select(group => new RuntimeAggregateRow(
                group.Sum(watchedEpisode => watchedEpisode.Episode!.RuntimeMinutes ?? 0),
                group.Count()))
            .SingleRowOrDefaultAsync(cancellationToken);

        return totals ?? new RuntimeAggregateRow(0, 0);
    }

    private Task<IReadOnlyList<InsightsShowCompletionData>> GetShowCompletionProjectionAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        InsightsV3SqlQueries.GetShowCompletionsAsync(dbContext, userId, cancellationToken);

    private async Task<DateTime?> GetNthMovieWatchedAtAsync(
        Guid userId,
        int ordinal,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .OrderBy(watchedMovie => watchedMovie.WatchedAt)
            .Skip(ordinal - 1)
            .Select(watchedMovie => (DateTime?)watchedMovie.WatchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DateTime?> GetNthEpisodeWatchedAtAsync(
        Guid userId,
        int ordinal,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .OrderBy(watchedEpisode => watchedEpisode.WatchedAt)
            .Skip(ordinal - 1)
            .Select(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DateTime?> GetNthRatingAtAsync(
        Guid userId,
        int ordinal,
        CancellationToken cancellationToken)
    {
        return await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .OrderBy(rating => rating.CreatedAt)
            .Skip(ordinal - 1)
            .Select(rating => (DateTime?)rating.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<T> ExecuteTimedQueryAsync<T>(
        InsightsSummaryQueryMetrics metrics,
        Func<Task<T>> query)
    {
        metrics.DbRoundTrips++;
        return await query();
    }

    private static async Task<T> ExecuteTimedQueryAsync<T>(
        InsightsAnalyticsQueryMetrics metrics,
        Func<Task<T>> query)
    {
        metrics.DbRoundTrips++;
        return await query();
    }

    private async Task<SummaryCountsRow> GetSummaryCountsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new SummaryCountsRow(
                user.CreatedAt,
                dbContext.WatchedMovies.Count(watchedMovie => watchedMovie.UserId == userId),
                dbContext.WatchedEpisodes.Count(watchedEpisode => watchedEpisode.UserId == userId),
                dbContext.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count(),
                dbContext.Ratings.Count(rating => rating.UserId == userId)))
            .FirstAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<(int Score, int Count)>> GetRatingScoreCountsAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .GroupBy(rating => rating.Score)
            .Select(group => new ValueTuple<int, int>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InsightsDnaTitleData>> GetMovieDnaTitlesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .Select(watchedMovie => new InsightsDnaTitleData(
                watchedMovie.Movie!.ReleaseDate.HasValue
                    ? watchedMovie.Movie.ReleaseDate.Value.Year
                    : null,
                watchedMovie.Movie!.MovieGenres
                    .Select(movieGenre => new InsightsDnaGenreData(
                        movieGenre.GenreId,
                        movieGenre.Genre.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    internal string GetTvShowDnaTitlesSql(Guid userId) =>
        TvShowDnaTitles(userId).ToQueryString();

    private async Task<IReadOnlyList<InsightsDnaTitleData>> GetTvShowDnaTitlesAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await TvShowDnaTitles(userId).ToListAsync(cancellationToken);

    private IQueryable<InsightsDnaTitleData> TvShowDnaTitles(Guid userId)
    {
        var watchedShowIds = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Distinct();

        return dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => watchedShowIds.Contains(tvShow.Id))
            .Select(tvShow => new InsightsDnaTitleData(
                tvShow.FirstAirDate.HasValue
                    ? tvShow.FirstAirDate.Value.Year
                    : null,
                tvShow.TvShowGenres
                    .Select(tvGenre => new InsightsDnaGenreData(
                        tvGenre.GenreId,
                        tvGenre.Genre.Name))
                    .ToList()));
    }

    public async Task<(InsightsV3RawData Raw, InsightsV3QueryMetrics Metrics)> GetV3RawDataAsync(
        Guid userId,
        TimeZoneInfo timeZone,
        int year,
        CancellationToken cancellationToken = default)
    {
        var metrics = new InsightsV3QueryMetrics();
        var dbStopwatch = Stopwatch.StartNew();
        var timeZoneId = timeZone.Id;

        var (currentYearStart, currentYearEnd) = GetCalendarYearUtcBounds(year, timeZone);
        var (previousYearStart, previousYearEnd) = GetCalendarYearUtcBounds(year - 1, timeZone);

        var summaryTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => GetV3SummaryAsync(context, userId, ct),
            cancellationToken);
        var movieWatchRowsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => GetMovieWatchProjectionRowsAsync(context, userId, ct),
            cancellationToken);
        var episodeWatchRowsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => GetEpisodeWatchProjectionRowsAsync(context, userId, ct),
            cancellationToken);
        var recordsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => InsightsV3SqlQueries.GetRecordsAsync(context, userId, timeZoneId, ct),
            cancellationToken);
        var runtimeTotalsTask = TimedScopedV3PgCommandAsync(
            metrics,
            async (context, ct) =>
            {
                var totals = await InsightsV3SqlQueries.GetRuntimeTotalsAsync(context, userId, ct);

                return new V3RuntimeTotalsRow(
                    totals.MovieTotalMinutes,
                    totals.MovieKnownCount,
                    totals.EpisodeTotalMinutes,
                    totals.EpisodeKnownCount);
            },
            cancellationToken);
        var ratingScoreCountsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => GetRatingScoreCountsAsync(context, userId, ct),
            cancellationToken);
        var genreRatingsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => InsightsV3SqlQueries.GetGenreRatingsAsync(context, userId, ct),
            cancellationToken);
        var oldestTitleTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => InsightsV3SqlQueries.GetOldestTitleAsync(context, userId, ct),
            cancellationToken);
        var showCompletionsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => InsightsV3SqlQueries.GetShowCompletionsAsync(context, userId, ct),
            cancellationToken);
        var milestoneTimestampsTask = TimedScopedV3PgCommandAsync(
            metrics,
            (context, ct) => InsightsV3SqlQueries.GetMilestoneTimestampsAsync(context, userId, ct),
            cancellationToken);

        await Task.WhenAll(
            summaryTask,
            movieWatchRowsTask,
            episodeWatchRowsTask,
            recordsTask,
            runtimeTotalsTask,
            ratingScoreCountsTask,
            genreRatingsTask,
            oldestTitleTask,
            showCompletionsTask,
            milestoneTimestampsTask);

        var (summary, summaryMs) = await summaryTask;
        var (movieWatchRows, movieWatchMs) = await movieWatchRowsTask;
        var (episodeWatchRows, episodeWatchMs) = await episodeWatchRowsTask;
        var (records, recordsMs) = await recordsTask;
        var (runtimeTotals, runtimeMs) = await runtimeTotalsTask;
        var (ratingScoreCounts, ratingScoreMs) = await ratingScoreCountsTask;
        var (genreRatings, genreRatingsMs) = await genreRatingsTask;
        var (oldestTitle, _) = await oldestTitleTask;
        var (showCompletions, showCompletionsMs) = await showCompletionsTask;
        var (milestoneTimestamps, milestoneTimestampsMs) = await milestoneTimestampsTask;

        metrics.SummaryMs = summaryMs;
        metrics.DnaMs = Math.Max(movieWatchMs, episodeWatchMs);
        metrics.RecordsMs = recordsMs;
        metrics.RuntimeMs = runtimeMs;
        metrics.RatingsMs = Math.Max(ratingScoreMs, genreRatingsMs);
        metrics.MilestonesMs = Math.Max(showCompletionsMs, milestoneTimestampsMs);

        var yearActivityStopwatch = Stopwatch.StartNew();
        var movieTitles = InsightsV3DnaProjections.ToAllTimeMovieTitles(movieWatchRows);
        var tvShowTitles = InsightsV3DnaProjections.ToAllTimeTvShowTitles(episodeWatchRows);
        var (currentYearMovieTitles, previousYearMovieTitles) = InsightsV3DnaProjections.SplitMovieTitlesByYear(
            movieWatchRows,
            previousYearStart,
            previousYearEnd,
            currentYearStart,
            currentYearEnd);
        var (currentYearTvShowTitles, previousYearTvShowTitles) = InsightsV3DnaProjections.SplitTvShowTitlesByYear(
            episodeWatchRows,
            previousYearStart,
            previousYearEnd,
            currentYearStart,
            currentYearEnd);
        var yearMovieWatches = InsightsV3DnaProjections.ToYearMovieWatches(
            movieWatchRows,
            currentYearStart,
            currentYearEnd);
        var yearEpisodeWatches = InsightsV3DnaProjections.ToYearEpisodeWatches(
            episodeWatchRows,
            currentYearStart,
            currentYearEnd);
        yearActivityStopwatch.Stop();
        metrics.YearActivityMs = yearActivityStopwatch.ElapsedMilliseconds;

        var milestoneRaw = new InsightsAnalyticsRawData(
            summary.MemberSince,
            summary.MoviesWatched,
            summary.EpisodesWatched,
            summary.ShowsStarted,
            summary.RatingsCount,
            [],
            movieTitles,
            tvShowTitles,
            runtimeTotals.MovieTotalMinutes,
            runtimeTotals.MovieKnownCount,
            runtimeTotals.EpisodeTotalMinutes,
            runtimeTotals.EpisodeKnownCount,
            ratingScoreCounts,
            showCompletions,
            milestoneTimestamps.FirstMovieWatchedAt,
            milestoneTimestamps.TenthMovieWatchedAt,
            milestoneTimestamps.FiftiethMovieWatchedAt,
            milestoneTimestamps.HundredthEpisodeWatchedAt,
            milestoneTimestamps.FiveHundredthEpisodeWatchedAt,
            milestoneTimestamps.TenthRatingAt,
            milestoneTimestamps.TwentyFifthRatingAt,
            milestoneTimestamps.FiftiethRatingAt);

        dbStopwatch.Stop();
        metrics.DbTotalMs = dbStopwatch.ElapsedMilliseconds;

        var raw = new InsightsV3RawData(
            summary.MemberSince,
            summary.DistinctMovieCount,
            summary.DistinctSeriesCount,
            summary.MoviesWatched,
            summary.EpisodesWatched,
            summary.ShowsStarted,
            summary.RatingsCount,
            movieTitles,
            tvShowTitles,
            currentYearMovieTitles,
            currentYearTvShowTitles,
            previousYearMovieTitles,
            previousYearTvShowTitles,
            yearMovieWatches,
            yearEpisodeWatches,
            records,
            runtimeTotals.MovieTotalMinutes,
            runtimeTotals.EpisodeTotalMinutes,
            runtimeTotals.MovieKnownCount,
            runtimeTotals.EpisodeKnownCount,
            ratingScoreCounts,
            genreRatings,
            oldestTitle,
            milestoneRaw);

        return (raw, metrics);
    }

    private static async Task<V3SummaryRow> GetV3SummaryAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new V3SummaryRow(
                user.CreatedAt,
                context.WatchedMovies.Count(watchedMovie => watchedMovie.UserId == userId),
                context.WatchedEpisodes.Count(watchedEpisode => watchedEpisode.UserId == userId),
                context.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count(),
                context.Ratings.Count(rating => rating.UserId == userId),
                context.WatchedMovies
                    .Where(watchedMovie => watchedMovie.UserId == userId)
                    .Select(watchedMovie => watchedMovie.MovieId)
                    .Distinct()
                    .Count(),
                context.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count()))
            .FirstAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<InsightsV3DnaProjections.MovieWatchRow>> GetMovieWatchProjectionRowsAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .Select(watchedMovie => new InsightsV3DnaProjections.MovieWatchRow(
                watchedMovie.WatchedAt,
                watchedMovie.Movie!.RuntimeMinutes,
                watchedMovie.Movie.ReleaseDate.HasValue
                    ? watchedMovie.Movie.ReleaseDate.Value.Year
                    : null,
                watchedMovie.Movie.MovieGenres
                    .Select(movieGenre => new InsightsDnaGenreData(
                        movieGenre.GenreId,
                        movieGenre.Genre.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<InsightsV3DnaProjections.EpisodeWatchRow>> GetEpisodeWatchProjectionRowsAsync(
        ApplicationDbContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .Select(watchedEpisode => new InsightsV3DnaProjections.EpisodeWatchRow(
                watchedEpisode.WatchedAt,
                watchedEpisode.Episode!.RuntimeMinutes,
                watchedEpisode.Episode.Season.TvShowId,
                watchedEpisode.Episode.Season.TvShow.FirstAirDate.HasValue
                    ? watchedEpisode.Episode.Season.TvShow.FirstAirDate.Value.Year
                    : null,
                watchedEpisode.Episode.Season.TvShow.TvShowGenres
                    .Select(tvGenre => new InsightsDnaGenreData(
                        tvGenre.GenreId,
                        tvGenre.Genre.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    private async Task<(IReadOnlyList<InsightsDnaTitleData> CurrentYear, IReadOnlyList<InsightsDnaTitleData> PreviousYear)>
        GetMovieDnaTitlesSplitByYearAsync(
            Guid userId,
            DateTime previousYearStart,
            DateTime previousYearEnd,
            DateTime currentYearStart,
            DateTime currentYearEnd,
            CancellationToken cancellationToken)
    {
        var watches = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie =>
                watchedMovie.UserId == userId &&
                watchedMovie.WatchedAt >= previousYearStart &&
                watchedMovie.WatchedAt < currentYearEnd)
            .Select(watchedMovie => new
            {
                watchedMovie.WatchedAt,
                Title = new InsightsDnaTitleData(
                    watchedMovie.Movie!.ReleaseDate.HasValue
                        ? watchedMovie.Movie.ReleaseDate.Value.Year
                        : null,
                    watchedMovie.Movie!.MovieGenres
                        .Select(movieGenre => new InsightsDnaGenreData(
                            movieGenre.GenreId,
                            movieGenre.Genre.Name))
                        .ToList()),
            })
            .ToListAsync(cancellationToken);

        return (
            watches
                .Where(watch => watch.WatchedAt >= currentYearStart && watch.WatchedAt < currentYearEnd)
                .Select(watch => watch.Title)
                .ToList(),
            watches
                .Where(watch => watch.WatchedAt >= previousYearStart && watch.WatchedAt < previousYearEnd)
                .Select(watch => watch.Title)
                .ToList());
    }

    private async Task<(IReadOnlyList<InsightsDnaTitleData> CurrentYear, IReadOnlyList<InsightsDnaTitleData> PreviousYear)>
        GetTvShowDnaTitlesSplitByYearAsync(
            Guid userId,
            DateTime previousYearStart,
            DateTime previousYearEnd,
            DateTime currentYearStart,
            DateTime currentYearEnd,
            CancellationToken cancellationToken)
    {
        var watches = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.WatchedAt >= previousYearStart &&
                watchedEpisode.WatchedAt < currentYearEnd)
            .Select(watchedEpisode => new
            {
                watchedEpisode.WatchedAt,
                Title = new InsightsDnaTitleData(
                    watchedEpisode.Episode!.Season.TvShow.FirstAirDate.HasValue
                        ? watchedEpisode.Episode.Season.TvShow.FirstAirDate.Value.Year
                        : null,
                    watchedEpisode.Episode.Season.TvShow.TvShowGenres
                        .Select(tvGenre => new InsightsDnaGenreData(
                            tvGenre.GenreId,
                            tvGenre.Genre.Name))
                        .ToList()),
            })
            .ToListAsync(cancellationToken);

        return (
            watches
                .Where(watch => watch.WatchedAt >= currentYearStart && watch.WatchedAt < currentYearEnd)
                .Select(watch => watch.Title)
                .ToList(),
            watches
                .Where(watch => watch.WatchedAt >= previousYearStart && watch.WatchedAt < previousYearEnd)
                .Select(watch => watch.Title)
                .ToList());
    }

    private static (DateTime UtcStartInclusive, DateTime UtcEndExclusive) GetCalendarYearUtcBounds(
        int year,
        TimeZoneInfo timeZone)
    {
        var localStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEndExclusive = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, timeZone));
    }

    private async Task<IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)>> GetMovieWatchesInRangeAsync(
        Guid userId,
        DateTime utcStartInclusive,
        DateTime utcEndExclusive,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie =>
                watchedMovie.UserId == userId &&
                watchedMovie.WatchedAt >= utcStartInclusive &&
                watchedMovie.WatchedAt < utcEndExclusive)
            .Select(watchedMovie => new ValueTuple<DateTime, int?>(
                watchedMovie.WatchedAt,
                watchedMovie.Movie!.RuntimeMinutes))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)>> GetEpisodeWatchesInRangeAsync(
        Guid userId,
        DateTime utcStartInclusive,
        DateTime utcEndExclusive,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.WatchedAt >= utcStartInclusive &&
                watchedEpisode.WatchedAt < utcEndExclusive)
            .Select(watchedEpisode => new ValueTuple<DateTime, int?>(
                watchedEpisode.WatchedAt,
                watchedEpisode.Episode!.RuntimeMinutes))
            .ToListAsync(cancellationToken);
    }

    private static async Task<T> ExecuteTimedV3PhaseAsync<T>(
        InsightsV3QueryMetrics metrics,
        Func<InsightsV3QueryMetrics, Task<T>> phase)
    {
        metrics.DbRoundTrips++;
        return await phase(metrics);
    }

    private static async Task<T> ExecuteTimedV3PgCommandAsync<T>(
        InsightsV3QueryMetrics metrics,
        Func<Task<T>> command)
    {
        metrics.DbRoundTrips++;
        return await ExecutePgCommandAsync(metrics, command);
    }

    private static async Task<T> ExecutePgCommandAsync<T>(
        InsightsV3QueryMetrics metrics,
        Func<Task<T>> command)
    {
        metrics.PgCommandRoundTrips++;
        return await command();
    }

    private static async Task<(T Result, long ElapsedMs)> TimedScopedV3PgCommandAsync<T>(
        InsightsV3QueryMetrics metrics,
        IServiceScopeFactory scopeFactory,
        Func<ApplicationDbContext, CancellationToken, Task<T>> command,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        IncrementV3PgCommandMetrics(metrics);
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var result = await command(context, cancellationToken);
        stopwatch.Stop();
        return (result, stopwatch.ElapsedMilliseconds);
    }

    private Task<(T Result, long ElapsedMs)> TimedScopedV3PgCommandAsync<T>(
        InsightsV3QueryMetrics metrics,
        Func<ApplicationDbContext, CancellationToken, Task<T>> command,
        CancellationToken cancellationToken) =>
        TimedScopedV3PgCommandAsync(metrics, scopeFactory, command, cancellationToken);

    private static void IncrementV3PgCommandMetrics(InsightsV3QueryMetrics metrics)
    {
        lock (metrics)
        {
            metrics.DbRoundTrips++;
            metrics.PgCommandRoundTrips++;
        }
    }
}
