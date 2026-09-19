using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class ResendPasswordResetEmailSenderTests
{
    private const string ResetUrl = "movieapp://reset-password?token=raw-token-value";

    [Fact]
    public async Task SendPasswordResetEmailAsyncUsesStableIdempotencyKeyAcrossRetries()
    {
        var tokenId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler);

        await sender.SendPasswordResetEmailAsync(
            tokenId,
            "user@example.com",
            ResetUrl,
            ContentLocaleResolver.EnglishUnitedStates);
        await sender.SendPasswordResetEmailAsync(
            tokenId,
            "user@example.com",
            ResetUrl,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(
            handler.Requests,
            request => Assert.Equal(
                "password-reset/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                request.Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task SendPasswordResetEmailAsyncRequestContainsEnglishHtmlTextPayloadAndResetUrl()
    {
        const string apiKey = "re_test_api_key_value";
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler, apiKey);

        await sender.SendPasswordResetEmailAsync(
            Guid.NewGuid(),
            "user@example.com",
            ResetUrl,
            ContentLocaleResolver.EnglishUnitedStates);

        var request = handler.Requests.Single();
        var body = await request.Content!.ReadAsStringAsync();

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(apiKey, request.Headers.Authorization?.Parameter);
        Assert.DoesNotContain(apiKey, body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal("Movie Cave <noreply@movieapp.test>", root.GetProperty("from").GetString());
        Assert.Equal("user@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal(MovieCavePasswordResetEmailContent.Subject, root.GetProperty("subject").GetString());

        var text = root.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains(ResetUrl, text, StringComparison.Ordinal);
        Assert.Contains("Reset your password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify your email address", text, StringComparison.Ordinal);

        var html = root.GetProperty("html").GetString();
        Assert.NotNull(html);
        Assert.Contains($"href=\"{ResetUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("Reset Password &rarr;", html, StringComparison.Ordinal);
        Assert.Contains("Let's get you back in.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsyncUsesTurkishSubjectAndCopyForTrLocale()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var sender = CreateSender(handler);

        await sender.SendPasswordResetEmailAsync(
            Guid.NewGuid(),
            "user@example.com",
            ResetUrl,
            ContentLocaleResolver.TurkishTurkey);

        var body = await handler.Requests.Single().Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal(
            "Movie Cave şifreni sıfırla",
            root.GetProperty("subject").GetString());

        var text = root.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("Hesabına tekrar erişelim.", text, StringComparison.Ordinal);
        Assert.Contains("Şifreni sıfırla:", text, StringComparison.Ordinal);

        var html = root.GetProperty("html").GetString();
        Assert.NotNull(html);
        Assert.Contains("lang=\"tr\"", html, StringComparison.Ordinal);
        Assert.Contains("Şifremi Sıfırla &rarr;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Reset Password &rarr;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsyncThrowsOnProviderFailureForRetry()
    {
        var handler = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sender = CreateSender(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendPasswordResetEmailAsync(
                Guid.NewGuid(),
                "user@example.com",
                ResetUrl,
                ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public void VerificationEmailSenderRemainsSeparateFromPasswordResetDelivery()
    {
        Assert.False(typeof(IPasswordResetEmailSender).IsAssignableFrom(typeof(ResendVerificationEmailSender)));
        Assert.False(typeof(IEmailVerificationEmailSender).IsAssignableFrom(typeof(ResendPasswordResetEmailSender)));
        Assert.False(typeof(IEmailVerificationEmailSender).IsAssignableFrom(typeof(SmtpEmailSender)));
    }

    private static ResendPasswordResetEmailSender CreateSender(
        HttpMessageHandler handler,
        string apiKey = "re_test_api_key_value",
        string publicBaseUrl = "") =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
            Options.Create(new ResendPasswordResetEmailOptions
            {
                ApiKey = apiKey,
                FromAddress = "noreply@movieapp.test",
                FromName = "Movie Cave"
            }),
            Options.Create(new PasswordResetOptions
            {
                DeliveryTimeoutSeconds = 30,
                BaseUrl = "movieapp://reset-password"
            }),
            Options.Create(new AppOptions
            {
                PublicBaseUrl = publicBaseUrl
            }),
            NullLogger<ResendPasswordResetEmailSender>.Instance);

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
