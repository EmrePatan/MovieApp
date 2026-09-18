using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class InsightsRepository(ApplicationDbContext dbContext) : IInsightsRepository
{
    private sealed record SummaryCountsRow(
        DateTime MemberSince,
        int MoviesWatched,
        int EpisodesWatched,
        int ShowsStarted,
        int RatingsCount);

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
            () => GetRatingScoreCountsAsync(userId, cancellationToken));

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
            () => GetRatingScoreCountsAsync(userId, cancellationToken));
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
        var rows = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId)
            .Select(watchedMovie => watchedMovie.Movie!.RuntimeMinutes)
            .ToListAsync(cancellationToken);

        var known = rows.Where(runtime => runtime is > 0).ToList();
        return new RuntimeAggregateRow(known.Sum(runtime => runtime ?? 0), known.Count);
    }

    private async Task<RuntimeAggregateRow> GetEpisodeRuntimeAggregateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId)
            .Select(watchedEpisode => watchedEpisode.Episode!.RuntimeMinutes)
            .ToListAsync(cancellationToken);

        var known = rows.Where(runtime => runtime is > 0).ToList();
        return new RuntimeAggregateRow(known.Sum(runtime => runtime ?? 0), known.Count);
    }

    private async Task<IReadOnlyList<InsightsShowCompletionData>> GetShowCompletionProjectionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.UserId == userId &&
                                  watchedEpisode.Episode.Season.TvShowId == tvShow.Id))
            .Select(tvShow => new InsightsShowCompletionData(
                tvShow.Seasons
                    .Where(season => season.SeasonNumber >= 1)
                    .SelectMany(season => season.Episodes)
                    .Count(),
                dbContext.WatchedEpisodes.Count(
                    watchedEpisode => watchedEpisode.UserId == userId &&
                                      watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                      watchedEpisode.Episode.Season.SeasonNumber >= 1),
                dbContext.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId &&
                                             watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                                             watchedEpisode.Episode.Season.SeasonNumber >= 1)
                    .Max(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt)))
            .ToListAsync(cancellationToken);
    }

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

    private async Task<IReadOnlyList<(int Score, int Count)>> GetRatingScoreCountsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Ratings
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

    private async Task<IReadOnlyList<InsightsDnaTitleData>> GetTvShowDnaTitlesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => dbContext.WatchedEpisodes.Any(
                watchedEpisode => watchedEpisode.UserId == userId &&
                                  watchedEpisode.Episode.Season.TvShowId == tvShow.Id))
            .Select(tvShow => new InsightsDnaTitleData(
                tvShow.FirstAirDate.HasValue
                    ? tvShow.FirstAirDate.Value.Year
                    : null,
                tvShow.TvShowGenres
                    .Select(tvGenre => new InsightsDnaGenreData(
                        tvGenre.GenreId,
                        tvGenre.Genre.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<(InsightsV3RawData Raw, InsightsV3QueryMetrics Metrics)> GetV3RawDataAsync(
        Guid userId,
        TimeZoneInfo timeZone,
        int year,
        CancellationToken cancellationToken = default)
    {
        var metrics = new InsightsV3QueryMetrics();
        var dbStopwatch = Stopwatch.StartNew();

        var (currentYearStart, currentYearEnd) = GetCalendarYearUtcBounds(year, timeZone);
        var (previousYearStart, previousYearEnd) = GetCalendarYearUtcBounds(year - 1, timeZone);

        var countsTask = ExecuteTimedV3QueryAsync(metrics, () => GetSummaryCountsAsync(userId, cancellationToken));
        var distinctMovieCountTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => dbContext.WatchedMovies
                .AsNoTracking()
                .Where(watchedMovie => watchedMovie.UserId == userId)
                .Select(watchedMovie => watchedMovie.MovieId)
                .Distinct()
                .CountAsync(cancellationToken));
        var distinctSeriesCountTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => dbContext.WatchedEpisodes
                .AsNoTracking()
                .Where(watchedEpisode => watchedEpisode.UserId == userId)
                .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                .Distinct()
                .CountAsync(cancellationToken));
        var movieTitlesTask = ExecuteTimedV3QueryAsync(metrics, () => GetMovieDnaTitlesAsync(userId, cancellationToken));
        var tvShowTitlesTask = ExecuteTimedV3QueryAsync(metrics, () => GetTvShowDnaTitlesAsync(userId, cancellationToken));
        var currentYearMovieTitlesTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetMovieDnaTitlesInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var currentYearTvShowTitlesTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetTvShowDnaTitlesInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var previousYearMovieTitlesTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetMovieDnaTitlesInRangeAsync(userId, previousYearStart, previousYearEnd, cancellationToken));
        var previousYearTvShowTitlesTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetTvShowDnaTitlesInRangeAsync(userId, previousYearStart, previousYearEnd, cancellationToken));
        var yearMovieWatchedAtTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetMovieWatchedAtInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var yearEpisodeWatchedAtTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetEpisodeWatchedAtInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var yearMovieWatchesTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetMovieWatchesInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var yearEpisodeWatchesTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => GetEpisodeWatchesInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var allMovieWatchedAtTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => dbContext.WatchedMovies
                .AsNoTracking()
                .Where(watchedMovie => watchedMovie.UserId == userId)
                .Select(watchedMovie => watchedMovie.WatchedAt)
                .ToListAsync(cancellationToken));
        var allEpisodeWatchedAtTask = ExecuteTimedV3QueryAsync(
            metrics,
            () => dbContext.WatchedEpisodes
                .AsNoTracking()
                .Where(watchedEpisode => watchedEpisode.UserId == userId)
                .Select(watchedEpisode => watchedEpisode.WatchedAt)
                .ToListAsync(cancellationToken));
        var movieRuntimeTask = ExecuteTimedV3QueryAsync(metrics, () => GetMovieRuntimeAggregateAsync(userId, cancellationToken));
        var episodeRuntimeTask = ExecuteTimedV3QueryAsync(metrics, () => GetEpisodeRuntimeAggregateAsync(userId, cancellationToken));
        var ratingScoreCountsTask = ExecuteTimedV3QueryAsync(metrics, () => GetRatingScoreCountsAsync(userId, cancellationToken));
        var genreRatingsTask = ExecuteTimedV3QueryAsync(metrics, () => GetGenreRatingsAsync(userId, cancellationToken));
        var oldestTitleTask = ExecuteTimedV3QueryAsync(metrics, () => GetOldestWatchedTitleAsync(userId, cancellationToken));
        var showCompletionsTask = ExecuteTimedV3QueryAsync(metrics, () => GetShowCompletionProjectionAsync(userId, cancellationToken));

        await Task.WhenAll(
            countsTask,
            distinctMovieCountTask,
            distinctSeriesCountTask,
            movieTitlesTask,
            tvShowTitlesTask,
            currentYearMovieTitlesTask,
            currentYearTvShowTitlesTask,
            previousYearMovieTitlesTask,
            previousYearTvShowTitlesTask,
            yearMovieWatchedAtTask,
            yearEpisodeWatchedAtTask,
            yearMovieWatchesTask,
            yearEpisodeWatchesTask,
            allMovieWatchedAtTask,
            allEpisodeWatchedAtTask,
            movieRuntimeTask,
            episodeRuntimeTask,
            ratingScoreCountsTask,
            genreRatingsTask,
            oldestTitleTask,
            showCompletionsTask);

        var counts = await countsTask;
        var distinctMovieCount = await distinctMovieCountTask;
        var distinctSeriesCount = await distinctSeriesCountTask;
        var movieRuntime = await movieRuntimeTask;
        var episodeRuntime = await episodeRuntimeTask;
        var ratingScoreCounts = await ratingScoreCountsTask;

        var firstMovieWatchedAt = counts.MoviesWatched >= 1
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthMovieWatchedAtAsync(userId, 1, cancellationToken))
            : null;
        var tenthMovieWatchedAt = counts.MoviesWatched >= 10
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthMovieWatchedAtAsync(userId, 10, cancellationToken))
            : null;
        var fiftiethMovieWatchedAt = counts.MoviesWatched >= 50
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthMovieWatchedAtAsync(userId, 50, cancellationToken))
            : null;
        var hundredthEpisodeWatchedAt = counts.EpisodesWatched >= 100
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthEpisodeWatchedAtAsync(userId, 100, cancellationToken))
            : null;
        var fiveHundredthEpisodeWatchedAt = counts.EpisodesWatched >= 500
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthEpisodeWatchedAtAsync(userId, 500, cancellationToken))
            : null;
        var tenthRatingAt = counts.RatingsCount >= 10
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthRatingAtAsync(userId, 10, cancellationToken))
            : null;
        var twentyFifthRatingAt = counts.RatingsCount >= 25
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthRatingAtAsync(userId, 25, cancellationToken))
            : null;
        var fiftiethRatingAt = counts.RatingsCount >= 50
            ? await ExecuteTimedV3QueryAsync(metrics, () => GetNthRatingAtAsync(userId, 50, cancellationToken))
            : null;

        var milestoneRaw = new InsightsAnalyticsRawData(
            counts.MemberSince,
            counts.MoviesWatched,
            counts.EpisodesWatched,
            counts.ShowsStarted,
            counts.RatingsCount,
            [],
            await movieTitlesTask,
            await tvShowTitlesTask,
            movieRuntime.TotalMinutes,
            movieRuntime.KnownCount,
            episodeRuntime.TotalMinutes,
            episodeRuntime.KnownCount,
            ratingScoreCounts,
            await showCompletionsTask,
            firstMovieWatchedAt,
            tenthMovieWatchedAt,
            fiftiethMovieWatchedAt,
            hundredthEpisodeWatchedAt,
            fiveHundredthEpisodeWatchedAt,
            tenthRatingAt,
            twentyFifthRatingAt,
            fiftiethRatingAt);

        dbStopwatch.Stop();
        metrics.DbTotalMs = dbStopwatch.ElapsedMilliseconds;

        var raw = new InsightsV3RawData(
            counts.MemberSince,
            distinctMovieCount,
            distinctSeriesCount,
            counts.MoviesWatched,
            counts.EpisodesWatched,
            counts.ShowsStarted,
            counts.RatingsCount,
            await movieTitlesTask,
            await tvShowTitlesTask,
            await currentYearMovieTitlesTask,
            await currentYearTvShowTitlesTask,
            await previousYearMovieTitlesTask,
            await previousYearTvShowTitlesTask,
            await yearMovieWatchedAtTask,
            await yearEpisodeWatchedAtTask,
            await yearMovieWatchesTask,
            await yearEpisodeWatchesTask,
            await allMovieWatchedAtTask,
            await allEpisodeWatchedAtTask,
            movieRuntime.TotalMinutes,
            episodeRuntime.TotalMinutes,
            movieRuntime.KnownCount,
            episodeRuntime.KnownCount,
            ratingScoreCounts,
            await genreRatingsTask,
            await oldestTitleTask,
            milestoneRaw);

        return (raw, metrics);
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

    private async Task<IReadOnlyList<InsightsDnaTitleData>> GetMovieDnaTitlesInRangeAsync(
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

    private async Task<IReadOnlyList<InsightsDnaTitleData>> GetTvShowDnaTitlesInRangeAsync(
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
            .Select(watchedEpisode => new InsightsDnaTitleData(
                watchedEpisode.Episode!.Season.TvShow.FirstAirDate.HasValue
                    ? watchedEpisode.Episode.Season.TvShow.FirstAirDate.Value.Year
                    : null,
                watchedEpisode.Episode.Season.TvShow.TvShowGenres
                    .Select(tvGenre => new InsightsDnaGenreData(
                        tvGenre.GenreId,
                        tvGenre.Genre.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DateTime>> GetMovieWatchedAtInRangeAsync(
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
            .Select(watchedMovie => watchedMovie.WatchedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DateTime>> GetEpisodeWatchedAtInRangeAsync(
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
            .Select(watchedEpisode => watchedEpisode.WatchedAt)
            .ToListAsync(cancellationToken);
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

    private async Task<IReadOnlyList<InsightsV3GenreRatingRow>> GetGenreRatingsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var movieGenreRatings = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId && rating.MovieId != null)
            .SelectMany(rating => rating.Movie!.MovieGenres.Select(movieGenre => new
            {
                movieGenre.GenreId,
                movieGenre.Genre.Name,
                rating.Score,
            }))
            .ToListAsync(cancellationToken);

        var tvGenreRatings = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId && rating.TvShowId != null)
            .SelectMany(rating => rating.TvShow!.TvShowGenres.Select(tvGenre => new
            {
                tvGenre.GenreId,
                tvGenre.Genre.Name,
                rating.Score,
            }))
            .ToListAsync(cancellationToken);

        return movieGenreRatings
            .Concat(tvGenreRatings)
            .GroupBy(item => new { item.GenreId, item.Name })
            .Select(group => new InsightsV3GenreRatingRow(
                group.Key.GenreId,
                group.Key.Name,
                group.Count(),
                group.Average(item => (decimal)item.Score)))
            .ToList();
    }

    private async Task<InsightsV3OldestTitleRow?> GetOldestWatchedTitleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var oldestMovie = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId && watchedMovie.Movie!.ReleaseDate != null)
            .OrderBy(watchedMovie => watchedMovie.Movie!.ReleaseDate)
            .Select(watchedMovie => new InsightsV3OldestTitleRow(
                "movie",
                watchedMovie.MovieId,
                watchedMovie.Movie!.Title,
                watchedMovie.Movie.ReleaseDate!.Value.Year,
                watchedMovie.Movie.PosterPath,
                watchedMovie.Movie.ReleaseDate))
            .FirstOrDefaultAsync(cancellationToken);

        var oldestTvShow = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode => watchedEpisode.UserId == userId &&
                                     watchedEpisode.Episode!.Season.TvShow.FirstAirDate != null)
            .OrderBy(watchedEpisode => watchedEpisode.Episode!.Season.TvShow.FirstAirDate)
            .Select(watchedEpisode => new InsightsV3OldestTitleRow(
                "tv",
                watchedEpisode.Episode!.Season.TvShowId,
                watchedEpisode.Episode.Season.TvShow.Title,
                watchedEpisode.Episode.Season.TvShow.FirstAirDate!.Value.Year,
                watchedEpisode.Episode.Season.TvShow.PosterPath,
                watchedEpisode.Episode.Season.TvShow.FirstAirDate))
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestMovie is null)
        {
            return oldestTvShow;
        }

        if (oldestTvShow is null)
        {
            return oldestMovie;
        }

        return oldestMovie.SortDate <= oldestTvShow.SortDate ? oldestMovie : oldestTvShow;
    }

    private static async Task<T> ExecuteTimedV3QueryAsync<T>(
        InsightsV3QueryMetrics metrics,
        Func<Task<T>> query)
    {
        metrics.DbRoundTrips++;
        return await query();
    }
}
