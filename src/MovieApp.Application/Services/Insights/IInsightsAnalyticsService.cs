using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public interface IInsightsAnalyticsService
{
    Task<InsightsAnalyticsResult> GetAnalyticsAsync(
        string timeZoneId,
        CancellationToken cancellationToken = default);
}
