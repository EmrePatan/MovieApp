using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class InsightsRepository(
    IServiceScopeFactory scopeFactory,
    IOptions<InsightsV3Options>? insightsV3Options = null) : IInsightsRepository
{
    private readonly int _maxV3RepositoryConcurrency =
        insightsV3Options?.Value.MaxRepositoryConcurrency ?? InsightsV3Options.DefaultMaxRepositoryConcurrency;

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

        using var concurrencyGate = new SemaphoreSlim(_maxV3RepositoryConcurrency, _maxV3RepositoryConcurrency);

        var summaryTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => GetV3SummaryAsync(context, userId, phaseCt),
                ct),
            cancellationToken);
        var yearActivityTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3AggregateQueries.GetYearActivityAsync(
                    context,
                    userId,
                    timeZoneId,
                    year,
                    currentYearStart,
                    currentYearEnd,
                    phaseCt),
                ct),
            cancellationToken);
        var genreContributionsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3AggregateQueries.GetGenreContributionsAsync(
                    context,
                    userId,
                    previousYearStart,
                    previousYearEnd,
                    currentYearStart,
                    currentYearEnd,
                    phaseCt),
                ct),
            cancellationToken);
        var titleFactsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3AggregateQueries.GetTitleFactsAsync(
                    context,
                    userId,
                    previousYearStart,
                    previousYearEnd,
                    currentYearStart,
                    currentYearEnd,
                    phaseCt),
                ct),
            cancellationToken);
        var recordsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3SqlQueries.GetRecordsAsync(context, userId, timeZoneId, phaseCt),
                ct),
            cancellationToken);
        var runtimeTotalsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                async (context, phaseCt) =>
                {
                    var totals = await InsightsV3SqlQueries.GetRuntimeTotalsAsync(context, userId, phaseCt);

                    return new V3RuntimeTotalsRow(
                        totals.MovieTotalMinutes,
                        totals.MovieKnownCount,
                        totals.EpisodeTotalMinutes,
                        totals.EpisodeKnownCount);
                },
                ct),
            cancellationToken);
        var ratingScoreCountsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => GetRatingScoreCountsAsync(context, userId, phaseCt),
                ct),
            cancellationToken);
        var genreRatingsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3SqlQueries.GetGenreRatingsAsync(context, userId, phaseCt),
                ct),
            cancellationToken);
        var oldestTitleTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3SqlQueries.GetOldestTitleAsync(context, userId, phaseCt),
                ct),
            cancellationToken);
        var showCompletionsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3SqlQueries.GetShowCompletionsAsync(context, userId, phaseCt),
                ct),
            cancellationToken);
        var milestoneTimestampsTask = InsightsV3RepositoryPhaseScheduler.RunBoundedAsync(
            concurrencyGate,
            ct => TimedScopedV3PgCommandAsync(
                metrics,
                (context, phaseCt) => InsightsV3SqlQueries.GetMilestoneTimestampsAsync(context, userId, phaseCt),
                ct),
            cancellationToken);

        await Task.WhenAll(
            summaryTask,
            yearActivityTask,
            genreContributionsTask,
            titleFactsTask,
            recordsTask,
            runtimeTotalsTask,
            ratingScoreCountsTask,
            genreRatingsTask,
            oldestTitleTask,
            showCompletionsTask,
            milestoneTimestampsTask);

        var (summary, summaryMs) = await summaryTask;
        var (yearActivity, yearActivityMs) = await yearActivityTask;
        var (genreContributions, genreContributionsMs) = await genreContributionsTask;
        var (titleFacts, titleFactsMs) = await titleFactsTask;
        var (records, recordsMs) = await recordsTask;
        var (runtimeTotals, runtimeMs) = await runtimeTotalsTask;
        var (ratingScoreCounts, ratingScoreMs) = await ratingScoreCountsTask;
        var (genreRatings, genreRatingsMs) = await genreRatingsTask;
        var (oldestTitle, _) = await oldestTitleTask;
        var (showCompletions, showCompletionsMs) = await showCompletionsTask;
        var (milestoneTimestamps, milestoneTimestampsMs) = await milestoneTimestampsTask;

        metrics.SummaryMs = summaryMs;
        metrics.DnaMs = Math.Max(genreContributionsMs, titleFactsMs);
        metrics.YearActivityMs = yearActivityMs;
        metrics.RecordsMs = recordsMs;
        metrics.RuntimeMs = runtimeMs;
        metrics.RatingsMs = Math.Max(ratingScoreMs, genreRatingsMs);
        metrics.MilestonesMs = Math.Max(showCompletionsMs, milestoneTimestampsMs);

        var distinctGenreCount = genreContributions.AllTime
            .Select(contribution => contribution.GenreId)
            .Distinct()
            .Count();

        var milestoneRaw = new InsightsAnalyticsRawData(
            summary.MemberSince,
            summary.MoviesWatched,
            summary.EpisodesWatched,
            summary.ShowsStarted,
            summary.RatingsCount,
            [],
            [],
            [],
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
            milestoneTimestamps.FiftiethRatingAt,
            distinctGenreCount);

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
            genreContributions.AllTime,
            titleFacts.AllTimeTitlesWithGenres,
            titleFacts.ReleaseYears,
            distinctGenreCount,
            genreContributions.CurrentYear,
            titleFacts.CurrentYearTitlesWithGenres,
            genreContributions.PreviousYear,
            titleFacts.PreviousYearTitlesWithGenres,
            yearActivity,
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
        var summary = await InsightsV3SqlQueries.GetV3SummaryAsync(context, userId, cancellationToken);

        return new V3SummaryRow(
            summary.MemberSince,
            summary.MoviesWatched,
            summary.EpisodesWatched,
            summary.ShowsStarted,
            summary.RatingsCount,
            summary.DistinctMovieCount,
            summary.DistinctSeriesCount);
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
