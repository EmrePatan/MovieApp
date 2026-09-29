using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

internal static class PendingEmailChangeIntentHelper
{
    public static async Task<string?> ResolveForUserAsync(
        IEmailVerificationTokenRepository emailVerificationTokenRepository,
        User user,
        CancellationToken cancellationToken)
    {
        var pendingEmail = await emailVerificationTokenRepository.GetPendingEmailChangeIntentForUserAsync(
            user.Id,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(pendingEmail))
        {
            return null;
        }

        var normalizedPending = UserEmailNormalizer.Normalize(pendingEmail);
        if (normalizedPending == user.NormalizedEmail)
        {
            return null;
        }

        return pendingEmail.Trim();
    }
}
