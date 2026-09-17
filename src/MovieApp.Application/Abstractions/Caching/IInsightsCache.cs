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

    Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
