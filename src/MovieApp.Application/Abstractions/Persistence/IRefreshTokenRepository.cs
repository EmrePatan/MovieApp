using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<Guid?> TryRevokeActiveTokenAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task RevokeAllActiveForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
