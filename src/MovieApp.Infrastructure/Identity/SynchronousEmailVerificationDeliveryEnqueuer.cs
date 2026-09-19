using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Infrastructure.Identity;

public sealed class SynchronousEmailVerificationDeliveryEnqueuer(
    IServiceScopeFactory serviceScopeFactory) : IEmailVerificationDeliveryEnqueuer
{
    public async Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<IEmailVerificationDeliveryService>();
        await deliveryService.DeliverAsync(tokenId, cancellationToken);
    }
}
