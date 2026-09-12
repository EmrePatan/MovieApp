using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository(ApplicationDbContext dbContext) : IPasswordResetTokenRepository
{
    public async Task<PasswordResetToken?> GetActiveByTokenHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PasswordResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash &&
                         token.UsedAtUtc == null &&
                         token.ExpiresAtUtc > utcNow,
                cancellationToken);
    }

    public async Task<PasswordResetTokenConsumptionResult?> TryConsumeActiveTokenAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var consumedRows = await dbContext.PasswordResetTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.UsedAtUtc, utcNow),
                cancellationToken);

        if (consumedRows == 0)
        {
            return null;
        }

        var consumedToken = await dbContext.PasswordResetTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => new { token.Id, token.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        return consumedToken is null
            ? null
            : new PasswordResetTokenConsumptionResult(consumedToken.Id, consumedToken.UserId);
    }

    public async Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        dbContext.PasswordResetTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateActiveTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await dbContext.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.UsedAtUtc, utcNow),
                cancellationToken);
    }
}
