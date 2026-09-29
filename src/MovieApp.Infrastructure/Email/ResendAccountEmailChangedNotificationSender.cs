using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Email;

public sealed class ResendAccountEmailChangedNotificationSender(
    HttpClient httpClient,
    IOptions<ResendVerificationEmailOptions> resendOptions,
    IOptions<SharedResendEmailOptions> sharedResendOptions,
    IOptions<EmailVerificationOptions> emailVerificationOptions,
    ILogger<ResendAccountEmailChangedNotificationSender> logger) : IAccountEmailChangedNotificationSender
{
    public async Task SendAsync(
        string previousEmail,
        string newEmail,
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
            ResendAccountEmailChangedNotificationLogMessages.LogSkippedNotConfigured(logger);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", effective.ApiKey);
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            $"account-email-changed:{previousEmail}:{newEmail}:{DateTime.UtcNow:yyyyMMddHHmm}");
        request.Content = JsonContent.Create(new ResendEmailRequest(
            $"{effective.FromName} <{effective.FromAddress}>",
            [previousEmail],
            MovieCaveAccountEmailChangedEmailContent.GetSubject(contentLocale),
            MovieCaveAccountEmailChangedEmailContent.BuildPlainText(contentLocale),
            null));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(5, emailVerificationOptions.Value.DeliveryTimeoutSeconds)));

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                ResendAccountEmailChangedNotificationLogMessages.LogDeliveryFailed(
                    logger,
                    (int)response.StatusCode);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ResendAccountEmailChangedNotificationLogMessages.LogDeliveryFailed(
                logger,
                exception.GetType().Name);
        }
    }

    private sealed record ResendEmailRequest(
        string From,
        IReadOnlyList<string> To,
        string Subject,
        string? Text,
        string? Html);
}
