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
    private const string VerifyUrl = "movieapp://verify-email?token=raw-token-value";

    [Fact]
    public async Task SendVerificationEmailAsyncUsesStableIdempotencyKeyAcrossRetries()
    {
        var tokenId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler);

        await sender.SendVerificationEmailAsync(tokenId, "user@example.com", VerifyUrl);
        await sender.SendVerificationEmailAsync(tokenId, "user@example.com", VerifyUrl);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(
            handler.Requests,
            request => Assert.Equal(
                "email-verification/11111111-2222-3333-4444-555555555555",
                request.Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task SendVerificationEmailAsyncRequestContainsHtmlTextPayloadAndCtaUrl()
    {
        const string apiKey = "re_test_api_key_value";
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler, apiKey);

        await sender.SendVerificationEmailAsync(
            Guid.NewGuid(),
            "user@example.com",
            VerifyUrl);

        var request = handler.Requests.Single();
        var body = await request.Content!.ReadAsStringAsync();

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(apiKey, request.Headers.Authorization?.Parameter);
        Assert.DoesNotContain(apiKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("protected_delivery_secret", body, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Movie Cave <noreply@movieapp.test>", root.GetProperty("from").GetString());
        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal(MovieCaveVerificationEmailContent.Subject, root.GetProperty("subject").GetString());

        var text = root.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains(VerifyUrl, text, StringComparison.Ordinal);
        Assert.Contains("Movie Cave", text, StringComparison.Ordinal);

        var html = root.GetProperty("html").GetString();
        Assert.NotNull(html);
        Assert.Contains($"href=\"{VerifyUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("Verify Email Address &rarr;", html, StringComparison.Ordinal);
        Assert.Contains("One more step to the good stuff.", html, StringComparison.Ordinal);
        Assert.Contains("Movie Cave", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendVerificationEmailAsyncIncludesConfiguredHeroImageInHtml()
    {
        const string heroImageUrl = "https://cdn.example.com/movie-cave/email-hero.jpg";
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler, heroImageUrl: heroImageUrl);

        await sender.SendVerificationEmailAsync(Guid.NewGuid(), "user@example.com", VerifyUrl);

        var body = await handler.Requests.Single().Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var html = document.RootElement.GetProperty("html").GetString();

        Assert.NotNull(html);
        Assert.Contains($"src=\"{heroImageUrl}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendVerificationEmailAsyncResolvesRelativeHeroImageUrlFromPublicBaseUrl()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(
            handler,
            heroImageUrl: VerificationEmailHeroUrlResolver.DefaultHeroImagePath,
            publicBaseUrl: "https://api.example.com");

        await sender.SendVerificationEmailAsync(Guid.NewGuid(), "user@example.com", VerifyUrl);

        var body = await handler.Requests.Single().Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var html = document.RootElement.GetProperty("html").GetString();

        Assert.NotNull(html);
        Assert.Contains(
            "src=\"https://api.example.com/email-assets/verification-hero-v2.jpg\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "src=\"https://api.example.com/email-assets/movie-cave-horizontal-logo-v1.png\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(MovieCaveVerificationEmailContent.HeaderLogoMarkerClass, html, StringComparison.Ordinal);
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
        string apiKey = "re_test_api_key_value",
        string? heroImageUrl = null,
        string publicBaseUrl = "") =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            Options.Create(new ResendVerificationEmailOptions
            {
                ApiKey = apiKey,
                FromAddress = "noreply@movieapp.test",
                FromName = "Movie Cave",
                HeroImageUrl = heroImageUrl ?? string.Empty
            }),
            Options.Create(new EmailVerificationOptions
            {
                DeliveryTimeoutSeconds = 30,
                BaseUrl = "movieapp://verify-email"
            }),
            Options.Create(new AppOptions
            {
                PublicBaseUrl = publicBaseUrl
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
