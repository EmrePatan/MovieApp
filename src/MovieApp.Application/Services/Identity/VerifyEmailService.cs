using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class VerifyEmailService(
    IApplicationDbContext applicationDbContext,
    IUserRepository userRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IAuthenticationSessionService authenticationSessionService,
    IAccountEmailChangedNotificationSender accountEmailChangedNotificationSender,
    ILogger<VerifyEmailService> logger) : IVerifyEmailService
{
    public const string InvalidTokenMessage = "Invalid or expired verification token.";

    public async Task<AuthenticationResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new ValidationException(InvalidTokenMessage);
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = PasswordResetTokenHasher.HashToken(request.Token.Trim());
        AuthenticationResult? result = null;
        string? previousEmailForNotification = null;
        string? completedPendingEmail = null;
        string? notificationContentLocale = null;

        await applicationDbContext.ExecuteInTransactionAsync(async ct =>
        {
            var activeToken = await emailVerificationTokenRepository.GetActiveTokenByHashAsync(
                tokenHash,
                utcNow,
                ct);

            if (activeToken is null)
            {
                throw new ValidationException(InvalidTokenMessage);
            }

            var user = await userRepository.GetByIdForUpdateAsync(activeToken.UserId, ct);
            if (user is null || !user.IsActive)
            {
                throw new ValidationException(InvalidTokenMessage);
            }

            if (!string.IsNullOrWhiteSpace(activeToken.PendingEmail))
            {
                if (!UserPendingEmailChange.TokenMatchesUserPendingEmail(user, activeToken.PendingEmail))
                {
                    throw new ValidationException(InvalidTokenMessage);
                }

                var pendingEmail = activeToken.PendingEmail.Trim();
                var normalizedPendingEmail = UserEmailNormalizer.Normalize(pendingEmail);
                if (await userRepository.ExistsByNormalizedEmailAsync(normalizedPendingEmail, ct))
                {
                    throw new ValidationException(InvalidTokenMessage);
                }

                if (!await emailVerificationTokenRepository.TryMarkTokenUsedAsync(activeToken.TokenId, utcNow, ct))
                {
                    throw new ValidationException(InvalidTokenMessage);
                }

                previousEmailForNotification = user.Email;
                completedPendingEmail = pendingEmail;
                notificationContentLocale = activeToken.ContentLocale;

                user.ChangeEmail(pendingEmail, normalizedPendingEmail, utcNow);
                user.ClearPendingEmailChange(utcNow);
                user.MarkEmailVerified(utcNow);
            }
            else
            {
                if (!await emailVerificationTokenRepository.TryMarkTokenUsedAsync(activeToken.TokenId, utcNow, ct))
                {
                    throw new ValidationException(InvalidTokenMessage);
                }

                if (!user.IsEmailVerified)
                {
                    user.MarkEmailVerified(utcNow);
                }
            }

            user.RecordSuccessfulLogin(utcNow);
            await userRepository.UpdateAsync(user, ct);

            if (!string.IsNullOrWhiteSpace(completedPendingEmail))
            {
                await authenticationSessionService.RevokeAllRefreshTokensForUserAsync(user.Id, ct);
            }

            result = await authenticationSessionService.IssueAsync(user, ct);
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousEmailForNotification) &&
            !string.IsNullOrWhiteSpace(completedPendingEmail))
        {
            try
            {
                await accountEmailChangedNotificationSender.SendAsync(
                    previousEmailForNotification,
                    completedPendingEmail,
                    notificationContentLocale ?? ContentLocaleResolver.EnglishUnitedStates,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                VerifyEmailLogMessages.LogAccountEmailChangedNotificationFailed(
                    logger,
                    exception.GetType().Name);
            }
        }

        return result!;
    }
}
