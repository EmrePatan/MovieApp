using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Email;

public sealed class ResendVerificationEmailSender(
    HttpClient httpClient,
    IOptions<ResendVerificationEmailOptions> resendOptions,
    IOptions<SharedResendEmailOptions> sharedResendOptions,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    IOptions<AppOptions> appOptions,
    ILogger<ResendVerificationEmailSender> logger) : IEmailVerificationEmailSender
{
    public async Task SendVerificationEmailAsync(
        Guid tokenId,
        string toEmail,
        string verifyUrl,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var effective = ResendEmailDeliverySettingsResolver.Resolve(
            resendOptions.Value.ApiKey,
            resendOptions.Value.FromAddress,
            resendOptions.Value.FromName,
            sharedResendOptions.Value);
        if (!effective.IsConfigured())
        {
            throw new InvalidOperationException(
                "Resend verification email sender is not configured. Set Authentication:Email:Resend or Authentication:EmailVerification:Resend.");
        }

        var heroImageUrl = VerificationEmailHeroUrlResolver.Resolve(
            resendOptions.Value.HeroImageUrl,
            appOptions.Value.PublicBaseUrl);
        var logoImageUrl = VerificationEmailLogoUrlResolver.Resolve(appOptions.Value.PublicBaseUrl);

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective.ApiKey);
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            ResendVerificationEmailIdempotency.CreateKey(tokenId));
        request.Content = JsonContent.Create(new ResendEmailRequest(
            $"{effective.FromName} <{effective.FromAddress}>",
            [toEmail],
            MovieCaveVerificationEmailContent.GetSubject(contentLocale),
            MovieCaveVerificationEmailContent.BuildPlainText(verifyUrl, contentLocale),
            MovieCaveVerificationEmailContent.BuildHtml(verifyUrl, heroImageUrl, logoImageUrl, contentLocale)));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(5, emailVerificationOptions.Value.DeliveryTimeoutSeconds)));

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var providerMessage = await ResendApiErrorReader.TryReadErrorMessageAsync(response, timeoutCts.Token);
                ResendVerificationEmailLogMessages.LogDeliveryFailed(
                    logger,
                    tokenId,
                    (int)response.StatusCode,
                    nameof(HttpRequestException),
                    providerMessage);

                throw new HttpRequestException(
                    $"Resend verification email delivery failed with status {(int)response.StatusCode}.");
            }

            ResendVerificationEmailLogMessages.LogDeliveryAccepted(logger, tokenId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResendVerificationEmailLogMessages.LogDeliveryFailed(
                logger,
                tokenId,
                0,
                nameof(TimeoutException),
                null);

            throw new TimeoutException("Resend verification email delivery timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ResendVerificationEmailLogMessages.LogDeliveryFailed(
                logger,
                tokenId,
                0,
                exception.GetType().Name,
                null);

            throw;
        }
    }

    private sealed record ResendEmailRequest(
        string From,
        IReadOnlyList<string> To,
        string Subject,
        string Text,
        string Html);
}
