using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiMovieRecommendationService
{
    Task<AiRecommendationServiceResult> GetRecommendationsAsync(
        Guid userId,
        string message,
        Guid? sessionId,
        string responseLanguage,
        CancellationToken cancellationToken = default);

    Task<int> GetRemainingQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
