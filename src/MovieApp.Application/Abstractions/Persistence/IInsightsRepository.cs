using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IInsightsRepository
{
    Task<(InsightsV3RawData Raw, InsightsV3QueryMetrics Metrics)> GetV3RawDataAsync(
        Guid userId,
        TimeZoneInfo timeZone,
        int year,
        CancellationToken cancellationToken = default);
}
