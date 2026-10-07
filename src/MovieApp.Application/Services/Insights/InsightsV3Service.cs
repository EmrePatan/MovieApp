using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
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
    IOptions<InsightsV3Options> insightsV3Options,
    InsightsV3LoadCoordinator loadCoordinator,
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
        var generation = await insightsCache.GetGenerationAsync(userId, cancellationToken);
        var cached = await insightsCache.GetV3Async(userId, timeZoneId, resolvedYear, cancellationToken);
        cacheLookupStopwatch.Stop();

        if (cached is not null)
        {
            totalStopwatch.Stop();
            InsightsV3LogMessages.LogCacheHit(
                logger,
                userId,
                totalStopwatch.ElapsedMilliseconds,
                cacheLookupStopwatch.ElapsedMilliseconds,
                resolvedYear,
                timeZoneId);
            return cached;
        }

        var cacheKey = InsightsCacheKeys.V3(userId, timeZoneId, resolvedYear, generation);
        // The shared build must not die when the first caller disconnects; other waiters
        // on the same generation still need the result, and the cache write has to finish.
        return await loadCoordinator.RunAsync(
            cacheKey,
            () => BuildAndCacheAsync(
                userId,
                timeZone,
                timeZoneId,
                resolvedYear,
                generation,
                utcNow,
                totalStopwatch,
                cacheLookupStopwatch.ElapsedMilliseconds,
                CancellationToken.None));
    }

    private async Task<InsightsV3Result> BuildAndCacheAsync(
        Guid userId,
        TimeZoneInfo timeZone,
        string timeZoneId,
        int resolvedYear,
        long generation,
        DateTime utcNow,
        Stopwatch totalStopwatch,
        long cacheLookupMs,
        CancellationToken cancellationToken)
    {
        var generationNow = await insightsCache.GetGenerationAsync(userId, cancellationToken);
        if (generationNow == generation)
        {
            var raced = await insightsCache.GetV3Async(userId, timeZoneId, resolvedYear, cancellationToken);
            if (raced is not null)
            {
                return raced;
            }
        }

        var (raw, metrics) = await insightsRepository.GetV3RawDataAsync(
            userId,
            timeZone,
            resolvedYear,
            cancellationToken);

        var buildStopwatch = Stopwatch.StartNew();
        var result = InsightsV3Builder.Build(raw, timeZone, resolvedYear, utcNow);
        buildStopwatch.Stop();

        var cacheWriteStopwatch = Stopwatch.StartNew();
        var ttl = TimeSpan.FromMinutes(options.Value.AnalyticsCacheTtlMinutes);
        var generationAfterBuild = await insightsCache.GetGenerationAsync(userId, cancellationToken);
        if (generationAfterBuild == generation)
        {
            await insightsCache.SetV3ForGenerationAsync(
                userId,
                timeZoneId,
                resolvedYear,
                generation,
                result,
                ttl,
                cancellationToken);
        }

        cacheWriteStopwatch.Stop();

        totalStopwatch.Stop();
        var sourceVersion = ApplicationSourceVersion.Resolve();
        InsightsV3LogMessages.LogCacheMiss(
            logger,
            userId,
            totalStopwatch.ElapsedMilliseconds,
            cacheLookupMs,
            metrics.DbTotalMs,
            metrics.DbRoundTrips,
            metrics.PgCommandRoundTrips,
            insightsV3Options.Value.MaxRepositoryConcurrency,
            metrics.SummaryMs,
            metrics.DnaMs,
            metrics.YearActivityMs,
            metrics.RecordsMs,
            metrics.RuntimeMs,
            metrics.RatingsMs,
            metrics.MilestonesMs,
            buildStopwatch.ElapsedMilliseconds,
            cacheWriteStopwatch.ElapsedMilliseconds,
            sourceVersion,
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
