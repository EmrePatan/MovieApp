using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Abstractions.Caching;

public interface IInsightsCache
{
    Task<InsightsV3Result?> GetV3Async(
        Guid userId,
        string? timeZoneId,
        int year,
        CancellationToken cancellationToken = default);

    Task<long> GetGenerationAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SetV3ForGenerationAsync(
        Guid userId,
        string? timeZoneId,
        int year,
        long generation,
        InsightsV3Result insights,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task SetV3Async(
        Guid userId,
        string? timeZoneId,
        int year,
        InsightsV3Result insights,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
