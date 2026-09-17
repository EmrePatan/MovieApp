using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiMovieRecommendationService
{
    Task<AiRecommendationServiceResult> GetRecommendationsAsync(
        Guid userId,
        string message,
        Guid? sessionId,
        CancellationToken cancellationToken = default);
}
