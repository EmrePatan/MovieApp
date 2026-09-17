using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Caching;

public sealed class InsightsCache(ICacheService cacheService) : IInsightsCache
{
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromDays(30);

    public async Task<InsightsSummaryResult?> GetSummaryAsync(
        Guid userId,
        string? timeZoneId,
        CancellationToken cancellationToken = default)
    {
        var generation = await GetGenerationAsync(userId, cancellationToken);
        var cacheKey = InsightsCacheKeys.Summary(userId, timeZoneId, generation);
        return await cacheService.GetAsync<InsightsSummaryResult>(cacheKey, cancellationToken);
    }

    public async Task SetSummaryAsync(
        Guid userId,
        string? timeZoneId,
        InsightsSummaryResult summary,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var generation = await GetGenerationAsync(userId, cancellationToken);
        var cacheKey = InsightsCacheKeys.Summary(userId, timeZoneId, generation);
        await cacheService.SetAsync(cacheKey, summary, ttl, cancellationToken);
    }

    public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var generationKey = InsightsCacheKeys.Generation(userId);
            var current = await cacheService.GetAsync<InsightsGenerationState>(
                generationKey,
                cancellationToken);
            var nextGeneration = (current?.Value ?? 0) + 1;
            await cacheService.SetAsync(
                generationKey,
                new InsightsGenerationState(nextGeneration),
                GenerationTtl,
                cancellationToken);
        }
        catch
        {
            // Cache invalidation must not fail the originating write operation.
        }
    }

    private async Task<long> GetGenerationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var generationKey = InsightsCacheKeys.Generation(userId);
        var current = await cacheService.GetAsync<InsightsGenerationState>(
            generationKey,
            cancellationToken);
        return current?.Value ?? 0;
    }
}
