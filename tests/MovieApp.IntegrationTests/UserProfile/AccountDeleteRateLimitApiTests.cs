using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieApp.Contracts.Users;
using MovieApp.Infrastructure.Persistence;
using MovieApp.IntegrationTests.Auth;

namespace MovieApp.IntegrationTests.UserProfile;

public sealed class AccountDeleteRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            UserProfileIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = UserProfileIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["Authentication:AccountRateLimit:AccountDeletionPermitLimit"] = "3";
            configuration["Authentication:AccountRateLimit:AccountDeletionWindowMinutes"] = "60";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

[CollectionDefinition("AccountDeleteRateLimitApi")]
public sealed class AccountDeleteRateLimitApiTestsFixture : ICollectionFixture<AccountDeleteRateLimitApiFixture>;

public sealed class AccountDeleteRateLimitApiFixture : IAsyncLifetime
{
    public AccountDeleteRateLimitWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(UserProfileIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("AccountDeleteRateLimitApi")]
public sealed class AccountDeleteRateLimitApiTests(AccountDeleteRateLimitApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task DeleteAccountReturns429AfterConfiguredLimit()
    {
        var token = await RegisterAndGetTokenAsync();

        var first = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            token,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));
        var second = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            token,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));
        var third = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            token,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));
        var fourth = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            token,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, third.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.True(fourth.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task DeleteAccountRateLimitPartitionsByAuthenticatedUser()
    {
        var firstUserToken = await RegisterAndGetTokenAsync();
        var secondUserToken = await RegisterAndGetTokenAsync();

        var firstUserFirstAttempt = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            firstUserToken,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));
        var secondUserFirstAttempt = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            secondUserToken,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));
        var firstUserSecondAttempt = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            firstUserToken,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));
        var secondUserSecondAttempt = await SendAuthorizedDeleteAsync(
            "/api/users/me",
            secondUserToken,
            new DeleteAccountRequest(CurrentPassword: "WrongPassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, firstUserFirstAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondUserFirstAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, firstUserSecondAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondUserSecondAttempt.StatusCode);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        return await AuthIntegrationHelpers.RegisterVerifyAndGetAccessTokenAsync(
            _client,
            fixture.Factory.Services,
            $"delete-rate-limit-{Guid.NewGuid():N}@example.com");
    }

    private Task<HttpResponseMessage> SendAuthorizedDeleteAsync(string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
