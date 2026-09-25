using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(ApplicationDbContext dbContext) : IRefreshTokenRepository
{
    public async Task CreateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid?> TryRevokeActiveTokenAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var revokedRows = await dbContext.RefreshTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, utcNow),
                cancellationToken);

        if (revokedRows == 0)
        {
            return null;
        }

        var userId = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return userId;
    }

    public async Task RevokeAllActiveForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await dbContext.RefreshTokens
            .Where(token =>
                token.UserId == userId &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, utcNow),
                cancellationToken);
    }
}
