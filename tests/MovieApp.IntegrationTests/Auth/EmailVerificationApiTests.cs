using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Email;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Auth;

[Collection("AuthApi")]
public sealed class EmailVerificationApiTests(AuthApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task RegisterCreatesUnverifiedUserWithoutJwtAndSendsVerificationEmail()
    {
        await fixture.ResetAsync();

        var email = $"register-{Guid.NewGuid():N}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Integration User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(payload);
        Assert.Equal(email, payload.Email);
        Assert.True(payload.RequiresEmailVerification);
        Assert.False(string.IsNullOrWhiteSpace(payload.Message));
        Assert.Single(fixture.Factory.EmailSender.SentVerificationEmails);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "StrongPassword123"));
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);

        var loginProblem = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(EmailNotVerifiedException.ErrorCode, loginProblem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task VerifyEmailReturnsJwtAndAllowsAuthenticatedAccess()
    {
        await fixture.ResetAsync();

        var email = $"verify-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var rawToken = fixture.Factory.EmailSender.ExtractTokenFromLastVerificationEmail();
        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(rawToken!));

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var authPayload = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authPayload);
        Assert.False(string.IsNullOrWhiteSpace(authPayload.AccessToken));

        var meResponse = await SendAuthorizedGetAsync("/api/auth/me", authPayload.AccessToken);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task VerifyEmailRejectsInvalidAndReplayedToken()
    {
        await fixture.ResetAsync();

        var invalidResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest("invalid-token"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var email = $"replay-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var rawToken = fixture.Factory.EmailSender.ExtractTokenFromLastVerificationEmail();
        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        var firstVerifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(rawToken!));
        Assert.Equal(HttpStatusCode.OK, firstVerifyResponse.StatusCode);

        var replayResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(rawToken!));
        Assert.Equal(HttpStatusCode.BadRequest, replayResponse.StatusCode);
    }

    [Fact]
    public async Task ResendVerificationInvalidatesPreviousToken()
    {
        await fixture.ResetAsync();

        var email = $"resend-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var firstToken = fixture.Factory.EmailSender.ExtractTokenFromLastVerificationEmail();
        Assert.False(string.IsNullOrWhiteSpace(firstToken));

        var resendResponse = await _client.PostAsJsonAsync(
            "/api/auth/resend-verification",
            new ResendVerificationRequest(email));
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);

        var secondToken = fixture.Factory.EmailSender.ExtractTokenFromLastVerificationEmail();
        Assert.False(string.IsNullOrWhiteSpace(secondToken));
        Assert.NotEqual(firstToken, secondToken);

        var firstVerifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(firstToken!));
        Assert.Equal(HttpStatusCode.BadRequest, firstVerifyResponse.StatusCode);

        var secondVerifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(secondToken!));
        Assert.Equal(HttpStatusCode.OK, secondVerifyResponse.StatusCode);
    }

    [Fact]
    public async Task ResendVerificationReturnsSameResponseForExistingAndMissingEmail()
    {
        await fixture.ResetAsync();

        var email = $"resend-generic-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var existingResponse = await _client.PostAsJsonAsync(
            "/api/auth/resend-verification",
            new ResendVerificationRequest(email));
        var missingResponse = await _client.PostAsJsonAsync(
            "/api/auth/resend-verification",
            new ResendVerificationRequest($"missing-{Guid.NewGuid():N}@example.com"));

        Assert.Equal(HttpStatusCode.OK, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);

        var existingPayload = await existingResponse.Content.ReadFromJsonAsync<MessageResponse>();
        var missingPayload = await missingResponse.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(existingPayload);
        Assert.NotNull(missingPayload);
        Assert.Equal(existingPayload.Message, missingPayload.Message);
    }

    [Fact]
    public async Task RegisterStoresHashedVerificationToken()
    {
        await fixture.ResetAsync();

        var email = $"hashed-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var rawToken = fixture.Factory.EmailSender.ExtractTokenFromLastVerificationEmail();
        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        await using var context = CreateContext();
        var storedToken = await context.EmailVerificationTokens.SingleAsync();
        Assert.Equal(PasswordResetTokenHasher.HashToken(rawToken!), storedToken.TokenHash);
        Assert.DoesNotContain(rawToken!, storedToken.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigratedVerifiedUserCanLoginWithoutVerificationFlow()
    {
        await fixture.ResetAsync();

        var email = $"migrated-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        await using var context = CreateContext();
        var user = await context.Users.SingleAsync(u => u.Email == email);
        user.EmailVerifiedAtUtc = user.CreatedAt;
        await context.SaveChangesAsync();

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "StrongPassword123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    private async Task RegisterUserAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Integration User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AuthIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
