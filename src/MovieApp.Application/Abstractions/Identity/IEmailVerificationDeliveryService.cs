namespace MovieApp.Application.Abstractions.Identity;

public interface IEmailVerificationDeliveryService
{
    Task DeliverAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
