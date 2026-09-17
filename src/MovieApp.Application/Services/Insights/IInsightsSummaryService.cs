using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public interface IInsightsSummaryService
{
    Task<InsightsSummaryResult> GetSummaryAsync(
        string? timeZoneId = null,
        CancellationToken cancellationToken = default);
}
