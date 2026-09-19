using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Email;

public sealed class ResendVerificationEmailSender(
    HttpClient httpClient,
    IOptions<ResendVerificationEmailOptions> resendOptions,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    ILogger<ResendVerificationEmailSender> logger) : IEmailVerificationEmailSender
{
    public async Task SendVerificationEmailAsync(
        Guid tokenId,
        string toEmail,
        string verifyUrl,
        CancellationToken cancellationToken = default)
    {
        var options = resendOptions.Value;
        if (!options.IsConfigured())
        {
            throw new InvalidOperationException(
                "Resend verification email sender is not configured. Set Authentication:EmailVerification:Resend.");
        }

        var heroImageUrl = string.IsNullOrWhiteSpace(options.HeroImageUrl)
            ? null
            : options.HeroImageUrl.Trim();

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            ResendVerificationEmailIdempotency.CreateKey(tokenId));
        request.Content = JsonContent.Create(new ResendEmailRequest(
            $"{options.FromName} <{options.FromAddress}>",
            [toEmail],
            MovieCaveVerificationEmailContent.Subject,
            MovieCaveVerificationEmailContent.BuildPlainText(verifyUrl),
            MovieCaveVerificationEmailContent.BuildHtml(verifyUrl, heroImageUrl)));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(5, emailVerificationOptions.Value.DeliveryTimeoutSeconds)));

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                ResendVerificationEmailLogMessages.LogDeliveryFailed(
                    logger,
                    tokenId,
                    (int)response.StatusCode,
                    nameof(HttpRequestException));

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
                nameof(TimeoutException));

            throw new TimeoutException("Resend verification email delivery timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ResendVerificationEmailLogMessages.LogDeliveryFailed(
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

    private sealed class ResendEmailResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }
}
