using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public sealed class VerifyEmailService(
    IApplicationDbContext applicationDbContext,
    IUserRepository userRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IAuthenticationSessionService authenticationSessionService) : IVerifyEmailService
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

        await applicationDbContext.ExecuteInTransactionAsync(async ct =>
        {
            var consumedToken = await emailVerificationTokenRepository.TryConsumeActiveTokenAsync(
                tokenHash,
                utcNow,
                ct);

            if (consumedToken is null)
            {
                throw new ValidationException(InvalidTokenMessage);
            }

            var user = await userRepository.GetByIdForUpdateAsync(consumedToken.UserId, ct);
            if (user is null || !user.IsActive)
            {
                throw new ValidationException(InvalidTokenMessage);
            }

            if (!user.IsEmailVerified)
            {
                user.MarkEmailVerified(utcNow);
            }

            user.RecordSuccessfulLogin(utcNow);
            await userRepository.UpdateAsync(user, ct);

            result = await authenticationSessionService.IssueAsync(user, ct);
        }, cancellationToken);

        return result!;
    }
}
