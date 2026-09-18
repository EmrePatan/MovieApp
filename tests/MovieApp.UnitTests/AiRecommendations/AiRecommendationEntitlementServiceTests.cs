using MovieApp.Infrastructure.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiRecommendationEntitlementServiceTests
{
    [Fact]
    public async Task EnsurePremiumEntitledAsyncAllowsAnyAuthenticatedUser()
    {
        var service = new AiRecommendationEntitlementService();

        var act = () => service.EnsurePremiumEntitledAsync(Guid.NewGuid());

        await act();
    }
}
