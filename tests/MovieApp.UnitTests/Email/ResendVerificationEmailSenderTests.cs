using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class ResendVerificationEmailSenderTests
{
    [Fact]
    public async Task SendVerificationEmailAsyncUsesStableIdempotencyKeyAcrossRetries()
    {
        var tokenId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler);

        await sender.SendVerificationEmailAsync(
            tokenId,
            "user@example.com",
            "movieapp://verify-email?token=raw-token-value");

        await sender.SendVerificationEmailAsync(
            tokenId,
            "user@example.com",
            "movieapp://verify-email?token=raw-token-value");

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(
            handler.Requests,
            request => Assert.Equal(
                "email-verification/11111111-2222-3333-4444-555555555555",
                request.Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task SendVerificationEmailAsyncRequestContainsOnlyRequiredEmailPayload()
    {
        const string apiKey = "re_test_api_key_value";
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler, apiKey);

        await sender.SendVerificationEmailAsync(
            Guid.NewGuid(),
            "user@example.com",
            "movieapp://verify-email?token=raw-token-value");

        var request = handler.Requests.Single();
        var body = await request.Content!.ReadAsStringAsync();

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(apiKey, request.Headers.Authorization?.Parameter);
        Assert.DoesNotContain(apiKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("protected_delivery_secret", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("MovieApp <noreply@movieapp.test>", root.GetProperty("from").GetString());
        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal("Verify your MovieApp email address", root.GetProperty("subject").GetString());
        Assert.Contains("movieapp://verify-email?token=raw-token-value", root.GetProperty("text").GetString());
    }

    [Fact]
    public void SmtpEmailSenderDoesNotExposeVerificationDeliveryMethod()
    {
        var verificationMethod = typeof(SmtpEmailSender).GetMethod(
            "SendEmailVerificationEmailAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        Assert.Null(verificationMethod);
        Assert.False(typeof(IEmailVerificationEmailSender).IsAssignableFrom(typeof(SmtpEmailSender)));
    }

    private static ResendVerificationEmailSender CreateSender(
        HttpMessageHandler handler,
        string apiKey = "re_test_api_key_value") =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            Options.Create(new ResendVerificationEmailOptions
            {
                ApiKey = apiKey,
                FromAddress = "noreply@movieapp.test",
                FromName = "MovieApp"
            }),
            Options.Create(new EmailVerificationOptions
            {
                DeliveryTimeoutSeconds = 30,
                BaseUrl = "movieapp://verify-email"
            }),
            NullLogger<ResendVerificationEmailSender>.Instance);

    private sealed class CapturingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var buffered = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
            {
                buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content is not null)
            {
                var body = request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                buffered.Content = new StringContent(body, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType ?? "application/json");
            }

            Requests.Add(buffered);
            return Task.FromResult(responder(request));
        }
    }
}
