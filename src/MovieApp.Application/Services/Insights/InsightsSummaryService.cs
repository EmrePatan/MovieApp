using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Application.Services.Insights;

public sealed class InsightsSummaryService(
    ICurrentUser currentUser,
    IInsightsRepository insightsRepository,
    IInsightsCache insightsCache,
    IOptions<InsightsOptions> options,
    ILogger<InsightsSummaryService> logger) : IInsightsSummaryService
{
    public async Task<InsightsSummaryResult> GetSummaryAsync(
        string? timeZoneId = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cached = await insightsCache.GetSummaryAsync(userId, timeZoneId, cancellationToken);
        cacheLookupStopwatch.Stop();

        if (cached is not null)
        {
            totalStopwatch.Stop();
            InsightsSummaryLogMessages.LogSummaryRequest(
                logger,
                "HIT",
                totalStopwatch.ElapsedMilliseconds,
                cacheLookupStopwatch.ElapsedMilliseconds,
                0,
                0,
                0,
                0,
                cached.Summary.MoviesWatched,
                cached.Summary.EpisodesWatched,
                cached.Summary.ShowsStarted,
                cached.Summary.RatingsCount,
                cached.MovieDna.Count);
            return cached;
        }

        var (raw, metrics) = await insightsRepository.GetSummaryRawDataAsync(userId, cancellationToken);

        var buildStopwatch = Stopwatch.StartNew();
        var utcNow = DateTime.UtcNow;
        var averageStarRating = ProfileStatisticsBuilder.CalculateAverageStarRating(raw.RatingScoreCounts);
        var movieDna = InsightsMovieDnaBuilder.Build(raw, utcNow);
        var summary = new InsightsSummaryResult(
            raw.MemberSince,
            movieDna,
            new InsightsSummaryStatsResult(
                raw.MoviesWatched,
                raw.EpisodesWatched,
                raw.ShowsStarted,
                raw.RatingsCount,
                averageStarRating),
            new InsightsWatchingMixResult(
                raw.MoviesWatched,
                raw.ShowsStarted),
            utcNow);
        buildStopwatch.Stop();

        var cacheWriteStopwatch = Stopwatch.StartNew();
        var ttl = TimeSpan.FromMinutes(options.Value.SummaryCacheTtlMinutes);
        await insightsCache.SetSummaryAsync(userId, timeZoneId, summary, ttl, cancellationToken);
        cacheWriteStopwatch.Stop();

        totalStopwatch.Stop();
        InsightsSummaryLogMessages.LogSummaryRequest(
            logger,
            "MISS",
            totalStopwatch.ElapsedMilliseconds,
            cacheLookupStopwatch.ElapsedMilliseconds,
            metrics.DbTotalMs,
            metrics.DbRoundTrips,
            buildStopwatch.ElapsedMilliseconds,
            cacheWriteStopwatch.ElapsedMilliseconds,
            summary.Summary.MoviesWatched,
            summary.Summary.EpisodesWatched,
            summary.Summary.ShowsStarted,
            summary.Summary.RatingsCount,
            summary.MovieDna.Count);

        return summary;
    }
}
