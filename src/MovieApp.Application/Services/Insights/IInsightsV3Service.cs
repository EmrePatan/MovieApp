using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public interface IInsightsV3Service
{
    Task<InsightsV3Result> GetInsightsV3Async(
        string timeZoneId,
        int? year,
        CancellationToken cancellationToken = default);
}
