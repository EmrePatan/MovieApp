using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Email;

public sealed class ResendPasswordResetEmailSender(
    HttpClient httpClient,
    IOptions<ResendPasswordResetEmailOptions> resendOptions,
    IOptions<PasswordResetOptions> passwordResetOptions,
    IOptions<AppOptions> appOptions,
    ILogger<ResendPasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public async Task SendPasswordResetEmailAsync(
        Guid tokenId,
        string toEmail,
        string resetUrl,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var options = resendOptions.Value;
        if (!options.IsConfigured())
        {
            throw new InvalidOperationException(
                "Resend password reset email sender is not configured. Set Authentication:PasswordReset:Resend.");
        }

        var heroImageUrl = VerificationEmailHeroUrlResolver.Resolve(
            string.IsNullOrWhiteSpace(options.HeroImageUrl)
                ? VerificationEmailHeroUrlResolver.DefaultHeroImagePath
                : options.HeroImageUrl,
            appOptions.Value.PublicBaseUrl);
        var logoImageUrl = VerificationEmailLogoUrlResolver.Resolve(appOptions.Value.PublicBaseUrl);

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            ResendPasswordResetEmailIdempotency.CreateKey(tokenId));
        request.Content = JsonContent.Create(new ResendEmailRequest(
            $"{options.FromName} <{options.FromAddress}>",
            [toEmail],
            MovieCavePasswordResetEmailContent.GetSubject(contentLocale),
            MovieCavePasswordResetEmailContent.BuildPlainText(resetUrl, contentLocale),
            MovieCavePasswordResetEmailContent.BuildHtml(resetUrl, heroImageUrl, logoImageUrl, contentLocale)));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(5, passwordResetOptions.Value.DeliveryTimeoutSeconds)));

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                ResendPasswordResetEmailLogMessages.LogDeliveryFailed(
                    logger,
                    tokenId,
                    (int)response.StatusCode,
                    nameof(HttpRequestException));

                throw new HttpRequestException(
                    $"Resend password reset email delivery failed with status {(int)response.StatusCode}.");
            }

            ResendPasswordResetEmailLogMessages.LogDeliveryAccepted(logger, tokenId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ResendPasswordResetEmailLogMessages.LogDeliveryFailed(
                logger,
                tokenId,
                0,
                nameof(TimeoutException));

            throw new TimeoutException("Resend password reset email delivery timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ResendPasswordResetEmailLogMessages.LogDeliveryFailed(
                logger,
                tokenId,
                0,
                exception.GetType().Name);

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
