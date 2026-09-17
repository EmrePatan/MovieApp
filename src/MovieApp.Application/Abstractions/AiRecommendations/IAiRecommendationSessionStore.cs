using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRecommendationSessionStore
{
    Task<AiRecommendationSessionState?> GetAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Guid userId,
        AiRecommendationSessionState session,
        CancellationToken cancellationToken = default);
}
