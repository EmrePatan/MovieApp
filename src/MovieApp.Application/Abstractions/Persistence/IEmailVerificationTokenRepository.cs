using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);

    Task InvalidateActiveTokensForUserAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}

public sealed record EmailVerificationTokenConsumptionResult(Guid TokenId, Guid UserId);
