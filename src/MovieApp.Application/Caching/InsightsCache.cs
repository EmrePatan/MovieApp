using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Caching;

public sealed class InsightsCache(ICacheService cacheService) : IInsightsCache
{
    public async Task<InsightsV3Result?> GetV3Async(
        Guid userId,
        string? timeZoneId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var generation = await GetGenerationAsync(userId, cancellationToken);
        var cacheKey = InsightsCacheKeys.V3(userId, timeZoneId, year, generation);
        return await cacheService.GetAsync<InsightsV3Result>(cacheKey, cancellationToken);
    }

    public Task<long> GetGenerationAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ReadGenerationAsync(userId, cancellationToken);

    public async Task SetV3ForGenerationAsync(
        Guid userId,
        string? timeZoneId,
        int year,
        long generation,
        InsightsV3Result insights,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = InsightsCacheKeys.V3(userId, timeZoneId, year, generation);
        await cacheService.SetAsync(cacheKey, insights, ttl, cancellationToken);
    }

    public async Task SetV3Async(
        Guid userId,
        string? timeZoneId,
        int year,
        InsightsV3Result insights,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var generation = await GetGenerationAsync(userId, cancellationToken);
        var cacheKey = InsightsCacheKeys.V3(userId, timeZoneId, year, generation);
        await cacheService.SetAsync(cacheKey, insights, ttl, cancellationToken);
    }

    public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        new InsightsCacheGeneration(cacheService).InvalidateForUserAsync(userId, cancellationToken);

    private async Task<long> ReadGenerationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var generationKey = InsightsCacheKeys.Generation(userId);
        var current = await cacheService.GetAsync<InsightsGenerationState>(
            generationKey,
            cancellationToken);
        return current?.Value ?? 0;
    }
}
