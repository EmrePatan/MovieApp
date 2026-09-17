using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public sealed class InsightsAnalyticsService(
    ICurrentUser currentUser,
    IInsightsRepository insightsRepository,
    IInsightsCache insightsCache,
    IOptions<InsightsOptions> options,
    ILogger<InsightsAnalyticsService> logger) : IInsightsAnalyticsService
{
    public async Task<InsightsAnalyticsResult> GetAnalyticsAsync(
        string timeZoneId,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var timeZone = InsightsTimeZoneGuard.RequireValidTimeZone(timeZoneId);

        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cached = await insightsCache.GetAnalyticsAsync(userId, timeZoneId, cancellationToken);
        cacheLookupStopwatch.Stop();

        if (cached is not null)
        {
            totalStopwatch.Stop();
            InsightsAnalyticsLogMessages.LogAnalyticsRequest(
                logger,
                "HIT",
                totalStopwatch.ElapsedMilliseconds,
                cacheLookupStopwatch.ElapsedMilliseconds,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                cached.Activity.Days.Count,
                cached.Taste.Genres.Count,
                cached.Milestones.Count);
            return cached;
        }

        var utcNow = DateTime.UtcNow;
        var activityUtcStart = InsightsTimeZoneGuard.GetActivityUtcStart(timeZone, utcNow);
        var (raw, metrics) = await insightsRepository.GetAnalyticsRawDataAsync(
            userId,
            activityUtcStart,
            cancellationToken);

        var buildStopwatch = Stopwatch.StartNew();
        var activity = InsightsActivityBuilder.Build(raw, timeZone, utcNow);
        var taste = InsightsTasteBuilder.Build(raw);
        var eras = InsightsErasBuilder.Build(raw);
        var estimatedTimeWatched = InsightsEstimatedTimeBuilder.Build(raw, timeZone, utcNow);
        var ratings = InsightsRatingsAnalyticsBuilder.Build(raw);
        var milestones = InsightsMilestonesBuilder.Build(raw);
        var result = new InsightsAnalyticsResult(
            activity,
            taste,
            eras,
            estimatedTimeWatched,
            ratings,
            milestones,
            utcNow);
        buildStopwatch.Stop();

        var cacheWriteStopwatch = Stopwatch.StartNew();
        var ttl = TimeSpan.FromMinutes(options.Value.AnalyticsCacheTtlMinutes);
        await insightsCache.SetAnalyticsAsync(userId, timeZoneId, result, ttl, cancellationToken);
        cacheWriteStopwatch.Stop();

        totalStopwatch.Stop();
        InsightsAnalyticsLogMessages.LogAnalyticsRequest(
            logger,
            "MISS",
            totalStopwatch.ElapsedMilliseconds,
            cacheLookupStopwatch.ElapsedMilliseconds,
            metrics.DbTotalMs,
            metrics.DbRoundTrips,
            metrics.ActivityMs,
            metrics.TasteErasMs,
            metrics.RuntimeMs,
            metrics.RatingsMs,
            metrics.MilestonesMs,
            buildStopwatch.ElapsedMilliseconds,
            cacheWriteStopwatch.ElapsedMilliseconds,
            result.Activity.Days.Count,
            result.Taste.Genres.Count,
            result.Milestones.Count);

        return result;
    }
}
