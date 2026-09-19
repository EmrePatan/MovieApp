using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class EmailVerificationTokenRepository(ApplicationDbContext dbContext) : IEmailVerificationTokenRepository
{
    public async Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var consumedRows = await dbContext.EmailVerificationTokens
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

        var consumedToken = await dbContext.EmailVerificationTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => new { token.Id, token.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        return consumedToken is null
            ? null
            : new EmailVerificationTokenConsumptionResult(consumedToken.Id, consumedToken.UserId);
    }

    public async Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
    {
        dbContext.EmailVerificationTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateActiveTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await dbContext.EmailVerificationTokens
            .Where(token =>
                token.UserId == userId &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.UsedAtUtc, utcNow)
                    .SetProperty(token => token.ProtectedDeliverySecret, (string?)null),
                cancellationToken);
    }

    public async Task<EmailVerificationDeliveryTarget?> GetDeliveryTargetAsync(
        Guid tokenId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailVerificationTokens
            .AsNoTracking()
            .Where(token => token.Id == tokenId)
            .Select(token => new EmailVerificationDeliveryTarget(
                token.Id,
                token.UserId,
                token.User.Email,
                token.ProtectedDeliverySecret ?? string.Empty,
                token.DeliveryCompletedAtUtc == null &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow &&
                token.ProtectedDeliverySecret != null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CompleteDeliveryAsync(
        Guid tokenId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await dbContext.EmailVerificationTokens
            .Where(token => token.Id == tokenId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.DeliveryCompletedAtUtc, utcNow)
                    .SetProperty(token => token.ProtectedDeliverySecret, (string?)null),
                cancellationToken);
    }
}
