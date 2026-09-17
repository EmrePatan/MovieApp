using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IInsightsRepository
{
    Task<(InsightsSummaryRawData Raw, InsightsSummaryQueryMetrics Metrics)> GetSummaryRawDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
