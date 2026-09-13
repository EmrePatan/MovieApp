using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Caching;

public sealed class ProfileStatisticsCache(ICacheService cacheService) : IProfileStatisticsCache
{
    public static readonly TimeSpan StatisticsTtl = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan GenerationTtl = TimeSpan.FromDays(30);

    public async Task<UserStatisticsResult?> GetAsync(
        Guid userId,
        string? timeZoneId,
        CancellationToken cancellationToken = default)
    {
        var generation = await GetGenerationAsync(userId, cancellationToken);
        var cacheKey = ProfileStatisticsCacheKeys.Create(userId, timeZoneId, generation);
        return await cacheService.GetAsync<UserStatisticsResult>(cacheKey, cancellationToken);
    }

    public async Task SetAsync(
        Guid userId,
        string? timeZoneId,
        UserStatisticsResult statistics,
        CancellationToken cancellationToken = default)
    {
        var generation = await GetGenerationAsync(userId, cancellationToken);
        var cacheKey = ProfileStatisticsCacheKeys.Create(userId, timeZoneId, generation);
        await cacheService.SetAsync(cacheKey, statistics, StatisticsTtl, cancellationToken);
    }

    public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var generationKey = ProfileStatisticsCacheKeys.Generation(userId);
            var current = await cacheService.GetAsync<ProfileStatisticsGenerationState>(
                generationKey,
                cancellationToken);
            var nextGeneration = (current?.Value ?? 0) + 1;
            await cacheService.SetAsync(
                generationKey,
                new ProfileStatisticsGenerationState(nextGeneration),
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
        var generationKey = ProfileStatisticsCacheKeys.Generation(userId);
        var current = await cacheService.GetAsync<ProfileStatisticsGenerationState>(
            generationKey,
            cancellationToken);
        return current?.Value ?? 0;
    }
}
