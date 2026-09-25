using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MovieApp.IntegrationTests.Auth;

[CollectionDefinition("AuthApi")]
public sealed class AuthApiTestsFixture : ICollectionFixture<AuthApiFixture>;

[Collection("AuthApi")]
public sealed class AuthApiTests(AuthApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task RegisterLoginAndGetCurrentUserSucceeds()
    {
        await fixture.ResetAsync();

        var email = $"user-{Guid.NewGuid():N}@example.com";
        var registerPayload = await AuthIntegrationHelpers.RegisterUserAsync(_client, email);
        Assert.True(registerPayload.RequiresEmailVerification);
        Assert.Equal(email, registerPayload.Email);

        var accessToken = await AuthIntegrationHelpers.VerifyLatestEmailAndGetAccessTokenAsync(
            _client,
            fixture.Factory.EmailSender);

        var meResponse = await SendAuthorizedGetAsync("/api/auth/me", accessToken);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var mePayload = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(mePayload);
        Assert.Equal(email, mePayload.Email);
        Assert.Equal("Integration User", mePayload.DisplayName);
    }

    [Fact]
    public async Task LoginReturnsTokenForRegisteredUser()
    {
        await fixture.ResetAsync();

        var email = $"login-{Guid.NewGuid():N}@example.com";
        await AuthIntegrationHelpers.RegisterUserAsync(_client, email);
        await AuthIntegrationHelpers.VerifyLatestEmailAndGetAccessTokenAsync(
            _client,
            fixture.Factory.EmailSender);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            email,
            "StrongPassword123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginPayload);
        Assert.False(string.IsNullOrWhiteSpace(loginPayload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(loginPayload.RefreshToken));
    }

    [Fact]
    public async Task RefreshTokenRotatesAndInvalidatesPreviousRefreshToken()
    {
        await fixture.ResetAsync();

        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        await AuthIntegrationHelpers.RegisterUserAsync(_client, email);
        await AuthIntegrationHelpers.VerifyLatestEmailAndGetAccessTokenAsync(
            _client,
            fixture.Factory.EmailSender);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            email,
            "StrongPassword123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginPayload);

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginPayload.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshedPayload = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedPayload);
        Assert.False(string.IsNullOrWhiteSpace(refreshedPayload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshedPayload.RefreshToken));
        Assert.NotEqual(loginPayload.RefreshToken, refreshedPayload.RefreshToken);

        var meResponse = await SendAuthorizedGetAsync("/api/auth/me", refreshedPayload.AccessToken);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var staleRefreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(loginPayload.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, staleRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task RegisterDuplicateEmailReturnsSameSuccessResponseAsInitialRegistration()
    {
        await fixture.ResetAsync();

        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var initialPayload = await AuthIntegrationHelpers.RegisterUserAsync(_client, email);

        var duplicateResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Another User"));

        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);

        var duplicatePayload = await duplicateResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(duplicatePayload);
        Assert.Equal(initialPayload.Email, duplicatePayload.Email);
        Assert.Equal(initialPayload.RequiresEmailVerification, duplicatePayload.RequiresEmailVerification);
        Assert.Equal(initialPayload.Message, duplicatePayload.Message);
    }

    [Fact]
    public async Task LoginWithInvalidCredentialsReturnsUnauthorized()
    {
        await fixture.ResetAsync();

        var email = $"invalid-{Guid.NewGuid():N}@example.com";
        await AuthIntegrationHelpers.RegisterUserAsync(_client, email);
        await AuthIntegrationHelpers.VerifyLatestEmailAndGetAccessTokenAsync(
            _client,
            fixture.Factory.EmailSender);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            email,
            "WrongPassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserWithoutTokenReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserWithInvalidTokenReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.value");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserWithExpiredTokenReturnsUnauthorized()
    {
        var expiredToken = CreateExpiredToken();

        var response = await SendAuthorizedGetAsync("/api/auth/me", expiredToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExistingMovieSearchEndpointStillWorksWithoutAuthentication()
    {
        var response = await _client.GetAsync("/api/movies/search?q=interstellar");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExistingTvShowSearchEndpointStillWorksWithoutAuthentication()
    {
        var response = await _client.GetAsync("/api/tvshows/search?q=breaking");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAuthorizedGetAsync(HttpClient client, string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAuthorizedGetAsync(string url, string token) =>
        SendAuthorizedGetAsync(_client, url, token);

    private static string CreateExpiredToken()
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "MovieApp",
            Audience = "MovieApp.Mobile",
            SigningKey = IntegrationTestJwtSettings.SigningKey,
            AccessTokenMinutes = 60
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var utcNow = DateTime.UtcNow.AddMinutes(-5);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "expired@example.com")
            ],
            notBefore: utcNow.AddMinutes(-10),
            expires: utcNow,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
