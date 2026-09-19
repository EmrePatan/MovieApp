using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.Identity;

public sealed class PasswordResetDeliveryService(
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IPasswordResetDeliverySecretProtector deliverySecretProtector,
    IPasswordResetEmailSender passwordResetEmailSender,
    IOptions<PasswordResetOptions> passwordResetOptions,
    ILogger<PasswordResetDeliveryService> logger) : IPasswordResetDeliveryService
{
    public async Task DeliverAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var target = await passwordResetTokenRepository.GetDeliveryTargetAsync(
            tokenId,
            utcNow,
            cancellationToken);

        if (target is null)
        {
            PasswordResetLogMessages.LogDeliveryTargetMissing(logger, tokenId);
            return;
        }

        if (!target.IsDeliverable)
        {
            PasswordResetLogMessages.LogDeliverySkippedNotDeliverable(
                logger,
                tokenId,
                target.UserId);
            return;
        }

        var rawToken = deliverySecretProtector.Unprotect(target.ProtectedDeliverySecret);
        var resetUrl = ForgotPasswordService.BuildResetUrl(
            passwordResetOptions.Value.BaseUrl,
            rawToken);

        try
        {
            await passwordResetEmailSender.SendPasswordResetEmailAsync(
                tokenId,
                target.Email,
                resetUrl,
                target.ContentLocale,
                cancellationToken);

            await passwordResetTokenRepository.CompleteDeliveryAsync(tokenId, utcNow, cancellationToken);

            PasswordResetLogMessages.LogDeliverySucceeded(logger, tokenId, target.UserId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            PasswordResetLogMessages.LogDeliveryAttemptFailed(
                logger,
                tokenId,
                target.UserId,
                exception.GetType().Name);

            throw;
        }
    }
}
