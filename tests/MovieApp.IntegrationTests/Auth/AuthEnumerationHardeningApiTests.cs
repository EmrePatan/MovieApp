using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Services.Identity;
using MovieApp.Contracts.Auth;
using MovieApp.Domain.Users;
using MovieApp.Infrastructure.Persistence;
using MovieApp.IntegrationTests.Support;

namespace MovieApp.IntegrationTests.Auth;

[Collection("AuthApi")]
public sealed class AuthEnumerationHardeningApiTests(AuthApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task SocialAuthForPasswordBackedEmailReturnsUnauthorizedWithoutConflict()
    {
        await fixture.ResetAsync();

        await AuthIntegrationHelpers.RegisterUserAsync(
            _client,
            IntegrationTestGoogleIdentityTokenVerifier.Email);

        var response = await _client.PostAsJsonAsync(
            "/api/auth/social",
            new SocialAuthRequest(
                ExternalLoginProviders.Google,
                IntegrationTestGoogleIdentityTokenVerifier.ValidToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Social authentication failed.", problem.GetProperty("detail").GetString());
        Assert.Equal("AUTHENTICATION_FAILED", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RegisterDuplicateVerifiedEmailDoesNotCreateSecondUser()
    {
        await fixture.ResetAsync();

        var email = $"verified-duplicate-{Guid.NewGuid():N}@example.com";
        await AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.EmailSender,
            email);

        var duplicateResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "AnotherPassword123",
            "Another User"));

        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);

        var duplicatePayload = await duplicateResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(duplicatePayload);
        Assert.Equal(email, duplicatePayload.Email);
        Assert.True(duplicatePayload.RequiresEmailVerification);
        Assert.Equal(RegisterUserService.VerificationRequiredMessage, duplicatePayload.Message);

        await using var context = await CreateContextAsync();
        var userCount = await context.Users.CountAsync(
            user => user.NormalizedEmail == UserEmailNormalizer.Normalize(email));
        Assert.Equal(1, userCount);
    }

    private static Task<ApplicationDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AuthIntegrationDatabase.GetConnectionString())
            .Options;

        return Task.FromResult(new ApplicationDbContext(options));
    }
}
