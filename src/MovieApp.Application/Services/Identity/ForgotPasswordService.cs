using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MovieApp.Application.Services.Identity;

public sealed class ForgotPasswordService(
    IUserRepository userRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetDeliverySecretProtector deliverySecretProtector,
    IPasswordResetDeliveryEnqueuer deliveryEnqueuer,
    IOptions<PasswordResetOptions> passwordResetOptions,
    ILogger<ForgotPasswordService> logger) : IForgotPasswordService
{
    public const string SuccessMessage =
        "If an account exists for this email, you will receive instructions to reset your password.";

    public async Task<MessageResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ForgotPasswordValidator.Validate(request.Email);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var normalizedEmail = UserEmailNormalizer.Normalize(request.Email);
        var user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is not null && user.IsActive)
        {
            var normalizedContentLocale = ContentLocaleResolver.ResolveFromAcceptLanguage(request.ContentLocale);
            var utcNow = DateTime.UtcNow;
            await passwordResetTokenRepository.InvalidateActiveTokensForUserAsync(
                user.Id,
                utcNow,
                cancellationToken);

            var rawToken = PasswordResetTokenGenerator.GenerateToken();
            var tokenHash = PasswordResetTokenHasher.HashToken(rawToken);
            var lifetime = TimeSpan.FromMinutes(Math.Max(1, passwordResetOptions.Value.TokenLifetimeMinutes));

            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                CreatedAtUtc = utcNow,
                ExpiresAtUtc = utcNow.Add(lifetime),
                ProtectedDeliverySecret = deliverySecretProtector.Protect(rawToken),
                ContentLocale = normalizedContentLocale
            };

            await passwordResetTokenRepository.CreateAsync(resetToken, cancellationToken);

            try
            {
                await deliveryEnqueuer.EnqueueAsync(resetToken.Id, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                PasswordResetLogMessages.LogDeliveryEnqueueFailed(
                    logger,
                    resetToken.Id,
                    user.Id,
                    exception.GetType().Name);
            }
        }

        return new MessageResult(SuccessMessage);
    }

    public static string BuildResetUrl(string baseUrl, string rawToken)
    {
        var trimmedBase = baseUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBase))
        {
            return $"?token={Uri.EscapeDataString(rawToken)}";
        }

        var separator = trimmedBase.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{trimmedBase}{separator}token={Uri.EscapeDataString(rawToken)}";
    }
}
