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
        var activeToken = await GetActiveTokenByHashAsync(tokenHash, utcNow, cancellationToken);
        if (activeToken is null)
        {
            return null;
        }

        var marked = await TryMarkTokenUsedAsync(activeToken.TokenId, utcNow, cancellationToken);
        return marked ? activeToken : null;
    }

    public async Task<EmailVerificationTokenConsumptionResult?> GetActiveTokenByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var activeToken = await dbContext.EmailVerificationTokens
            .AsNoTracking()
            .Where(token =>
                token.TokenHash == tokenHash &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .Select(token => new { token.Id, token.UserId, token.PendingEmail, token.ContentLocale })
            .FirstOrDefaultAsync(cancellationToken);

        return activeToken is null
            ? null
            : new EmailVerificationTokenConsumptionResult(
                activeToken.Id,
                activeToken.UserId,
                activeToken.PendingEmail,
                activeToken.ContentLocale);
    }

    public async Task<bool> TryMarkTokenUsedAsync(
        Guid tokenId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var consumedRows = await dbContext.EmailVerificationTokens
            .Where(token =>
                token.Id == tokenId &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.UsedAtUtc, utcNow),
                cancellationToken);

        return consumedRows == 1;
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
                token.PendingEmail ?? token.User.Email,
                token.ProtectedDeliverySecret ?? string.Empty,
                token.DeliveryCompletedAtUtc == null &&
                token.UsedAtUtc == null &&
                token.ExpiresAtUtc > utcNow &&
                token.ProtectedDeliverySecret != null,
                token.ContentLocale))
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

    public async Task InvalidateUnusedPendingEmailChangeTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await dbContext.EmailVerificationTokens
            .Where(token =>
                token.UserId == userId &&
                token.UsedAtUtc == null &&
                token.PendingEmail != null &&
                token.PendingEmail != string.Empty)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.UsedAtUtc, utcNow)
                    .SetProperty(token => token.ProtectedDeliverySecret, (string?)null),
                cancellationToken);
    }
}
