using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Identity;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Auth;

[Collection("AuthApi")]
public sealed class PasswordResetApiTests(AuthApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task ForgotPasswordReturnsSameResponseForExistingAndMissingEmail()
    {
        await fixture.ResetAsync();

        var email = $"reset-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var existingResponse = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        var missingResponse = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest($"missing-{Guid.NewGuid():N}@example.com"));

        Assert.Equal(HttpStatusCode.OK, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);

        var existingPayload = await existingResponse.Content.ReadFromJsonAsync<MessageResponse>();
        var missingPayload = await missingResponse.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(existingPayload);
        Assert.NotNull(missingPayload);
        Assert.Equal(existingPayload.Message, missingPayload.Message);
    }

    [Fact]
    public async Task ForgotPasswordStoresHashedTokenAndSendsEmail()
    {
        await fixture.ResetAsync();

        var email = $"hashed-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(fixture.Factory.EmailSender.SentEmails);

        var rawToken = fixture.Factory.EmailSender.ExtractTokenFromLastEmail();
        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        await using var context = CreateContext();
        var storedToken = await context.PasswordResetTokens.SingleAsync();
        Assert.Equal(PasswordResetTokenHasher.HashToken(rawToken!), storedToken.TokenHash);
        Assert.DoesNotContain(rawToken!, storedToken.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetPasswordChangesPasswordInvalidatesOldJwtAndConsumesToken()
    {
        await fixture.ResetAsync();

        var email = $"complete-{Guid.NewGuid():N}@example.com";
        var oldToken = await RegisterAndGetAccessTokenAsync(email);

        var forgotResponse = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);

        var resetToken = fixture.Factory.EmailSender.ExtractTokenFromLastEmail();
        Assert.False(string.IsNullOrWhiteSpace(resetToken));

        var resetResponse = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(resetToken!, "AnotherPassword123"));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var oldMeResponse = await SendAuthorizedGetAsync("/api/auth/me", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldMeResponse.StatusCode);

        var oldLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "StrongPassword123"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        var newLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "AnotherPassword123"));
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);

        var reuseResponse = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(resetToken!, "YetAnotherPassword123"));
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordRejectsInvalidToken()
    {
        await fixture.ResetAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest("invalid-token", "AnotherPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPasswordInvalidatesPreviousActiveToken()
    {
        await fixture.ResetAsync();

        var email = $"invalidate-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var firstToken = fixture.Factory.EmailSender.ExtractTokenFromLastEmail();
        Assert.False(string.IsNullOrWhiteSpace(firstToken));

        await _client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var secondToken = fixture.Factory.EmailSender.ExtractTokenFromLastEmail();
        Assert.False(string.IsNullOrWhiteSpace(secondToken));
        Assert.NotEqual(firstToken, secondToken);

        var firstResetResponse = await _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(firstToken!, "AnotherPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, firstResetResponse.StatusCode);
    }

    private async Task RegisterUserAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Integration User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<string> RegisterAndGetAccessTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Integration User"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
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
