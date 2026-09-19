using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Infrastructure.Identity;

public sealed class SynchronousPasswordResetDeliveryEnqueuer(
    IServiceScopeFactory serviceScopeFactory) : IPasswordResetDeliveryEnqueuer
{
    public async Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<IPasswordResetDeliveryService>();
        await deliveryService.DeliverAsync(tokenId, cancellationToken);
    }
}
