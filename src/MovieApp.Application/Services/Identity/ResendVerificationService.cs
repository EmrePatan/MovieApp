using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.Application.Services.Identity;

public sealed class ResendVerificationService(
    IUserRepository userRepository,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IEmailVerificationDeliverySecretProtector deliverySecretProtector,
    IEmailVerificationDeliveryEnqueuer deliveryEnqueuer,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    ILogger<ResendVerificationService> logger) : IResendVerificationService
{
    public const string SuccessMessage =
        "If an account exists for this email and requires verification, you will receive a verification email shortly.";

    public async Task<MessageResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ForgotPasswordValidator.Validate(request.Email);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var normalizedEmail = UserEmailNormalizer.Normalize(request.Email);
        var user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is not null && user.IsActive && user.HasPassword && !user.IsEmailVerified)
        {
            await SendVerificationEmailAsync(user, cancellationToken);
        }

        return new MessageResult(SuccessMessage);
    }

    public async Task SendVerificationEmailAsync(User user, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        await emailVerificationTokenRepository.InvalidateActiveTokensForUserAsync(
            user.Id,
            utcNow,
            cancellationToken);

        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var tokenHash = PasswordResetTokenHasher.HashToken(rawToken);
        var lifetime = TimeSpan.FromMinutes(Math.Max(1, emailVerificationOptions.Value.TokenLifetimeMinutes));

        var verificationToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.Add(lifetime),
            ProtectedDeliverySecret = deliverySecretProtector.Protect(rawToken)
        };

        await emailVerificationTokenRepository.CreateAsync(verificationToken, cancellationToken);

        try
        {
            await deliveryEnqueuer.EnqueueAsync(verificationToken.Id, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            EmailVerificationLogMessages.LogDeliveryEnqueueFailed(
                logger,
                verificationToken.Id,
                user.Id,
                exception.GetType().Name);
        }
    }
}
