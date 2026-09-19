namespace MovieApp.Application.Abstractions.Identity;

public interface IEmailVerificationDeliveryEnqueuer
{
    Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
