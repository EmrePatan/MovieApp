using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public sealed class InsightsV3Service(
    ICurrentUser currentUser,
    IInsightsRepository insightsRepository,
    IInsightsCache insightsCache,
    IOptions<InsightsOptions> options,
    ILogger<InsightsV3Service> logger) : IInsightsV3Service
{
    public async Task<InsightsV3Result> GetInsightsV3Async(
        string timeZoneId,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var timeZone = InsightsTimeZoneGuard.RequireValidTimeZone(timeZoneId);
        var utcNow = DateTime.UtcNow;
        var resolvedYear = InsightsV3TimeRangeHelper.ResolveYear(year, timeZone, utcNow);
        ValidateYear(resolvedYear, timeZone, utcNow);

        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cached = await insightsCache.GetV3Async(userId, timeZoneId, resolvedYear, cancellationToken);
        cacheLookupStopwatch.Stop();

        if (cached is not null)
        {
            totalStopwatch.Stop();
            InsightsV3LogMessages.LogCacheHit(
                logger,
                userId,
                totalStopwatch.ElapsedMilliseconds,
                resolvedYear,
                timeZoneId);
            return cached;
        }

        var (raw, metrics) = await insightsRepository.GetV3RawDataAsync(
            userId,
            timeZone,
            resolvedYear,
            cancellationToken);

        var buildStopwatch = Stopwatch.StartNew();
        var result = InsightsV3Builder.Build(raw, timeZone, resolvedYear, utcNow);
        buildStopwatch.Stop();

        var ttl = TimeSpan.FromMinutes(options.Value.AnalyticsCacheTtlMinutes);
        await insightsCache.SetV3Async(userId, timeZoneId, resolvedYear, result, ttl, cancellationToken);

        totalStopwatch.Stop();
        InsightsV3LogMessages.LogCacheMiss(
            logger,
            userId,
            totalStopwatch.ElapsedMilliseconds,
            metrics.DbTotalMs,
            buildStopwatch.ElapsedMilliseconds,
            metrics.DbRoundTrips,
            resolvedYear);

        return result;
    }

    private static void ValidateYear(int year, TimeZoneInfo timeZone, DateTime utcNow)
    {
        var currentLocalYear = InsightsV3TimeRangeHelper.ResolveYear(null, timeZone, utcNow);
        if (year < 1970 || year > currentLocalYear)
        {
            throw new ValidationException($"Year must be between 1970 and {currentLocalYear}.");
        }
    }
}
