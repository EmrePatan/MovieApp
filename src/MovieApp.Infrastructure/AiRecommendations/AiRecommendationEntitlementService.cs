using MovieApp.Application.Abstractions.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiRecommendationEntitlementService : IAiRecommendationEntitlementService
{
    public Task EnsurePremiumEntitledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // AI Recommendations V1 is available to every authenticated user.
        // Premium entitlement options and exception mapping remain for future premium-only features.
        return Task.CompletedTask;
    }
}
