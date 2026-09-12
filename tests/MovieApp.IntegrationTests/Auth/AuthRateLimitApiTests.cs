using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Auth;

public sealed class AuthRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            AuthIntegrationDatabase.GetRateLimitConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = AuthIntegrationDatabase.GetRateLimitConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["Authentication:RateLimit:LoginPermitLimit"] = "2";
            configuration["Authentication:RateLimit:LoginWindowMinutes"] = "1";
            configuration["Authentication:RateLimit:RegisterPermitLimit"] = "2";
            configuration["Authentication:RateLimit:RegisterWindowMinutes"] = "10";
            configuration["Authentication:RateLimit:ForgotPasswordPermitLimit"] = "2";
            configuration["Authentication:RateLimit:ForgotPasswordWindowMinutes"] = "15";
            configuration["Authentication:RateLimit:ResetPasswordPermitLimit"] = "2";
            configuration["Authentication:RateLimit:ResetPasswordWindowMinutes"] = "15";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

[CollectionDefinition("AuthRateLimitApi")]
public sealed class AuthRateLimitApiTestsFixture : ICollectionFixture<AuthRateLimitApiFixture>;

public sealed class AuthRateLimitApiFixture : IAsyncLifetime
{
    public AuthRateLimitWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task ResetAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Factory.Dispose();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(AuthIntegrationDatabase.GetRateLimitConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("AuthRateLimitApi")]
public sealed class AuthRateLimitApiTests(AuthRateLimitApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task LoginReturns429AfterConfiguredLimit()
    {
        var email = $"ratelimit-{Guid.NewGuid():N}@example.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Rate Limit User"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var first = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword123"));
        var second = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword123"));
        var third = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.True(third.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task ForgotPasswordReturns429AfterConfiguredLimit()
    {
        var first = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest("first@example.com"));
        var second = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest("second@example.com"));
        var third = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest("third@example.com"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
