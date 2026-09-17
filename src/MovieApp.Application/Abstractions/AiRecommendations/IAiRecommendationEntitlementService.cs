namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRecommendationEntitlementService
{
    Task EnsurePremiumEntitledAsync(Guid userId, CancellationToken cancellationToken = default);
}
