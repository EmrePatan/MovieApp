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
}
