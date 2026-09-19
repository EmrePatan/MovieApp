namespace MovieApp.Application.Abstractions.Identity;

public interface IPasswordResetDeliveryEnqueuer
{
    Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
