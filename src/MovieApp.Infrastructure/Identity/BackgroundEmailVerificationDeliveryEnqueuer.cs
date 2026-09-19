using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Infrastructure.Identity;

/// <summary>
/// Development-only convenience fallback when Hangfire is disabled locally.
/// </summary>
public sealed class BackgroundEmailVerificationDeliveryEnqueuer(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BackgroundEmailVerificationDeliveryEnqueuer> logger) : IEmailVerificationDeliveryEnqueuer
{
    public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IEmailVerificationDeliveryService>();
                await deliveryService.DeliverAsync(tokenId, CancellationToken.None);
            }
            catch (Exception exception)
            {
                EmailVerificationLogMessages.LogBackgroundDeliveryAttemptFailed(
                    logger,
                    tokenId,
                    exception.GetType().Name);
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }
}
