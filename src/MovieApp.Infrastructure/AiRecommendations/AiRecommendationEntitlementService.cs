using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiRecommendationEntitlementService(
    IOptions<AiRecommendationOptions> options,
    IHostEnvironment hostEnvironment) : IAiRecommendationEntitlementService
{
    public Task EnsurePremiumEntitledAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entitlement = options.Value.Entitlement;

        if (entitlement.PremiumUserIds.Contains(userId))
        {
            return Task.CompletedTask;
        }

        if (hostEnvironment.IsDevelopment() && entitlement.AllowDevelopmentBypass)
        {
            return Task.CompletedTask;
        }

        throw new AiRecommendationEntitlementException(
            "Premium subscription is required for AI movie recommendations.");
    }
}
