using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public sealed class VerifyEmailService(
    IApplicationDbContext applicationDbContext,
    IUserRepository userRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    ITokenService tokenService) : IVerifyEmailService
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

            var token = tokenService.CreateAccessToken(UserMapper.ToTokenUserContext(user));
            result = new AuthenticationResult(
                token.AccessToken,
                token.ExpiresAt,
                UserMapper.ToCurrentUserResult(user));
        }, cancellationToken);

        return result!;
    }
}
