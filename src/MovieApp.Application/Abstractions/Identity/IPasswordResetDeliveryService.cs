namespace MovieApp.Application.Abstractions.Identity;

public interface IPasswordResetDeliveryService
{
    Task DeliverAsync(Guid tokenId, CancellationToken cancellationToken = default);
}
