using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class ResetPasswordService(
    IApplicationDbContext applicationDbContext,
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordHasher passwordHasher,
    IAuthenticationSessionService authenticationSessionService) : IResetPasswordService
{
    public const string SuccessMessage =
        "Your password has been reset. You can now sign in with your new password.";

    public const string InvalidTokenMessage =
        "Invalid or expired reset token.";

    public async Task<MessageResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new ValidationException(InvalidTokenMessage);
        }

        var passwordValidation = PasswordPolicyValidator.Validate(request.NewPassword);
        if (!passwordValidation.IsValid)
        {
            throw new ValidationException(passwordValidation.ErrorMessage!);
        }

        var utcNow = DateTime.UtcNow;
        var tokenHash = PasswordResetTokenHasher.HashToken(request.Token.Trim());

        await applicationDbContext.ExecuteInTransactionAsync(async ct =>
        {
            var consumedToken = await passwordResetTokenRepository.TryConsumeActiveTokenAsync(
                tokenHash,
                utcNow,
                ct);

            if (consumedToken is null)
            {
                throw new ValidationException(InvalidTokenMessage);
            }

            var user = await userRepository.GetByIdForUpdateAsync(consumedToken.UserId, ct);
            if (user is null || !user.IsActive || !user.HasPassword)
            {
                throw new ValidationException(InvalidTokenMessage);
            }

            if (passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash!))
            {
                throw new ValidationException("New password must be different from the current password.");
            }

            var passwordHash = passwordHasher.HashPassword(request.NewPassword);
            user.ChangePassword(passwordHash, utcNow);

            await userRepository.UpdateAsync(user, ct);
            await passwordResetTokenRepository.InvalidateActiveTokensForUserAsync(user.Id, utcNow, ct);
            await authenticationSessionService.RevokeAllRefreshTokensForUserAsync(user.Id, ct);
        }, cancellationToken);

        return new MessageResult(SuccessMessage);
    }
}
