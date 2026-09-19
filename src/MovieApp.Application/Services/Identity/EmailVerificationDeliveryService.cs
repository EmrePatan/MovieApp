using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.Identity;

public sealed class EmailVerificationDeliveryService(
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IEmailVerificationDeliverySecretProtector deliverySecretProtector,
    IEmailVerificationEmailSender verificationEmailSender,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    ILogger<EmailVerificationDeliveryService> logger) : IEmailVerificationDeliveryService
{
    public async Task DeliverAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var target = await emailVerificationTokenRepository.GetDeliveryTargetAsync(
            tokenId,
            utcNow,
            cancellationToken);

        if (target is null)
        {
            EmailVerificationLogMessages.LogDeliveryTargetMissing(logger, tokenId);
            return;
        }

        if (!target.IsDeliverable)
        {
            EmailVerificationLogMessages.LogDeliverySkippedNotDeliverable(
                logger,
                tokenId,
                target.UserId);
            return;
        }

        var rawToken = deliverySecretProtector.Unprotect(target.ProtectedDeliverySecret);
        var verifyUrl = EmailVerificationUrlBuilder.BuildVerificationUrl(
            emailVerificationOptions.Value.BaseUrl,
            rawToken);

        try
        {
            await verificationEmailSender.SendVerificationEmailAsync(
                tokenId,
                target.Email,
                verifyUrl,
                cancellationToken);

            await emailVerificationTokenRepository.CompleteDeliveryAsync(tokenId, utcNow, cancellationToken);

            EmailVerificationLogMessages.LogDeliverySucceeded(logger, tokenId, target.UserId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            EmailVerificationLogMessages.LogDeliveryAttemptFailed(
                logger,
                tokenId,
                target.UserId,
                exception.GetType().Name);

            throw;
        }
    }
}
