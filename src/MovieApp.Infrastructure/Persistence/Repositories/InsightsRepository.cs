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
        var totals = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(watchedMovie => watchedMovie.UserId == userId && watchedMovie.Movie!.RuntimeMinutes > 0)
            .GroupBy(_ => 1)
            .Select(group => new RuntimeAggregateRow(
                group.Sum(watchedMovie => watchedMovie.Movie!.RuntimeMinutes ?? 0),
                group.Count()))
            .FirstOrDefaultAsync(cancellationToken);

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
            .FirstOrDefaultAsync(cancellationToken);

        return totals ?? new RuntimeAggregateRow(0, 0);
    }

    private async Task<V3RuntimeTotalsRow> GetV3RuntimeTotalsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var movieRuntime = await GetMovieRuntimeAggregateAsync(userId, cancellationToken);
        var episodeRuntime = await GetEpisodeRuntimeAggregateAsync(userId, cancellationToken);

        return new V3RuntimeTotalsRow(
            movieRuntime.TotalMinutes,
            movieRuntime.KnownCount,
            episodeRuntime.TotalMinutes,
            episodeRuntime.KnownCount);
    }

    private async Task<IReadOnlyList<InsightsShowCompletionData>> GetShowCompletionProjectionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.Episode!.Season.SeasonNumber >= 1)
            .GroupBy(watchedEpisode => watchedEpisode.Episode!.Season.TvShowId)
            .Select(group => new InsightsShowCompletionData(
                dbContext.Episodes.Count(episode =>
                    episode.Season.TvShowId == group.Key &&
                    episode.Season.SeasonNumber >= 1),
                group.Count(),
                group.Max(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt)))
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
        var timeZoneId = timeZone.Id;

        var (currentYearStart, currentYearEnd) = GetCalendarYearUtcBounds(year, timeZone);
        var (previousYearStart, previousYearEnd) = GetCalendarYearUtcBounds(year - 1, timeZone);

        var summaryStopwatch = Stopwatch.StartNew();
        var summary = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetV3SummaryAsync(userId, cancellationToken));
        summaryStopwatch.Stop();
        metrics.SummaryMs = summaryStopwatch.ElapsedMilliseconds;

        var dnaStopwatch = Stopwatch.StartNew();
        var movieTitles = await ExecuteTimedV3QueryAsync(metrics, () => GetMovieDnaTitlesAsync(userId, cancellationToken));
        var tvShowTitles = await ExecuteTimedV3QueryAsync(metrics, () => GetTvShowDnaTitlesAsync(userId, cancellationToken));
        var (currentYearMovieTitles, previousYearMovieTitles) = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetMovieDnaTitlesSplitByYearAsync(
                userId,
                previousYearStart,
                previousYearEnd,
                currentYearStart,
                currentYearEnd,
                cancellationToken));
        var (currentYearTvShowTitles, previousYearTvShowTitles) = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetTvShowDnaTitlesSplitByYearAsync(
                userId,
                previousYearStart,
                previousYearEnd,
                currentYearStart,
                currentYearEnd,
                cancellationToken));
        dnaStopwatch.Stop();
        metrics.DnaMs = dnaStopwatch.ElapsedMilliseconds;

        var yearActivityStopwatch = Stopwatch.StartNew();
        var yearMovieWatches = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetMovieWatchesInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        var yearEpisodeWatches = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetEpisodeWatchesInRangeAsync(userId, currentYearStart, currentYearEnd, cancellationToken));
        yearActivityStopwatch.Stop();
        metrics.YearActivityMs = yearActivityStopwatch.ElapsedMilliseconds;

        var recordsStopwatch = Stopwatch.StartNew();
        var records = await ExecuteTimedV3QueryAsync(
            metrics,
            () => InsightsV3SqlQueries.GetRecordsAsync(dbContext, userId, timeZoneId, cancellationToken));
        recordsStopwatch.Stop();
        metrics.RecordsMs = recordsStopwatch.ElapsedMilliseconds;

        var runtimeStopwatch = Stopwatch.StartNew();
        var runtimeTotals = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetV3RuntimeTotalsAsync(userId, cancellationToken));
        runtimeStopwatch.Stop();
        metrics.RuntimeMs = runtimeStopwatch.ElapsedMilliseconds;

        var ratingsStopwatch = Stopwatch.StartNew();
        var ratingScoreCounts = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetRatingScoreCountsAsync(userId, cancellationToken));
        var genreRatings = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetGenreRatingsAsync(userId, cancellationToken));
        ratingsStopwatch.Stop();
        metrics.RatingsMs = ratingsStopwatch.ElapsedMilliseconds;

        var oldestTitle = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetOldestWatchedTitleAsync(userId, cancellationToken));

        var milestonesStopwatch = Stopwatch.StartNew();
        var showCompletions = await ExecuteTimedV3QueryAsync(
            metrics,
            () => GetShowCompletionProjectionAsync(userId, cancellationToken));
        var milestoneTimestamps = await ExecuteTimedV3QueryAsync(
            metrics,
            () => InsightsV3SqlQueries.GetMilestoneTimestampsAsync(dbContext, userId, cancellationToken));
        milestonesStopwatch.Stop();
        metrics.MilestonesMs = milestonesStopwatch.ElapsedMilliseconds;

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

    private async Task<V3SummaryRow> GetV3SummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new V3SummaryRow(
                user.CreatedAt,
                dbContext.WatchedMovies.Count(watchedMovie => watchedMovie.UserId == userId),
                dbContext.WatchedEpisodes.Count(watchedEpisode => watchedEpisode.UserId == userId),
                dbContext.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count(),
                dbContext.Ratings.Count(rating => rating.UserId == userId),
                dbContext.WatchedMovies
                    .Where(watchedMovie => watchedMovie.UserId == userId)
                    .Select(watchedMovie => watchedMovie.MovieId)
                    .Distinct()
                    .Count(),
                dbContext.WatchedEpisodes
                    .Where(watchedEpisode => watchedEpisode.UserId == userId)
                    .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
                    .Distinct()
                    .Count()))
            .FirstAsync(cancellationToken);
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
