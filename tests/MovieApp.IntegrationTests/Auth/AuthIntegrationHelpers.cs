using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Email;

namespace MovieApp.IntegrationTests.Auth;

internal static class AuthIntegrationHelpers
{
    internal static async Task<RegisterResponse> RegisterUserAsync(
        HttpClient client,
        string email,
        string displayName = "Integration User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            displayName));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(payload);
        return payload;
    }

    internal static async Task<string> RegisterVerifyAndGetAccessTokenAsync(
        HttpClient client,
        CapturingEmailSender emailSender,
        string email)
    {
        await RegisterUserAsync(client, email);
        return await VerifyLatestEmailAndGetAccessTokenAsync(client, emailSender);
    }

    internal static async Task<string> RegisterVerifyAndGetAccessTokenAsync(
        HttpClient client,
        IServiceProvider services,
        string email,
        string displayName = "Integration User")
    {
        await RegisterUserAsync(client, email, displayName);
        var emailSender = services.GetRequiredService<CapturingEmailSender>();
        return await VerifyLatestEmailAndGetAccessTokenAsync(client, emailSender);
    }

    internal static async Task<string> VerifyLatestEmailAndGetAccessTokenAsync(
        HttpClient client,
        CapturingEmailSender emailSender)
    {
        var rawToken = emailSender.ExtractTokenFromLastVerificationEmail();
        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new VerifyEmailRequest(rawToken!));

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var authPayload = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authPayload);
        Assert.False(string.IsNullOrWhiteSpace(authPayload.AccessToken));
        return authPayload.AccessToken;
    }
}
