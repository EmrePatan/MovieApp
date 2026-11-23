using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public interface IInsightsV3Service
{
    Task<InsightsV3Result> GetInsightsV3Async(
        string timeZoneId,
        int? year,
        CancellationToken cancellationToken = default);

    /// <summary>Same snapshot for another user; callers must already have checked profile visibility.</summary>
    Task<InsightsV3Result> GetInsightsV3ForUserAsync(
        Guid userId,
        string timeZoneId,
        int? year,
        CancellationToken cancellationToken = default);
}
