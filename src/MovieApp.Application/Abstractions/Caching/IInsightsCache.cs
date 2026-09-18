using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Abstractions.Caching;

public interface IInsightsCache
{
    Task<InsightsSummaryResult?> GetSummaryAsync(
        Guid userId,
        string? timeZoneId,
        CancellationToken cancellationToken = default);

    Task SetSummaryAsync(
        Guid userId,
        string? timeZoneId,
        InsightsSummaryResult summary,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<InsightsAnalyticsResult?> GetAnalyticsAsync(
        Guid userId,
        string? timeZoneId,
        CancellationToken cancellationToken = default);

    Task SetAnalyticsAsync(
        Guid userId,
        string? timeZoneId,
        InsightsAnalyticsResult analytics,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<InsightsV3Result?> GetV3Async(
        Guid userId,
        string? timeZoneId,
        int year,
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
