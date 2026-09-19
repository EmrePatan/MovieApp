using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetActiveByTokenHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically marks an active token as used. Returns the token id and user id when successful.
    /// </summary>
    Task<PasswordResetTokenConsumptionResult?> TryConsumeActiveTokenAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task InvalidateActiveTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<PasswordResetDeliveryTarget?> GetDeliveryTargetAsync(
        Guid tokenId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task CompleteDeliveryAsync(
        Guid tokenId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

public sealed record PasswordResetTokenConsumptionResult(Guid TokenId, Guid UserId);
