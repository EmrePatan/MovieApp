using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieApp.Contracts.Auth;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Errors;

public sealed class ExceptionHandlingRateLimitWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            ExceptionHandlingRateLimitIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                ExceptionHandlingRateLimitIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["Authentication:PasswordReset:TokenLifetimeMinutes"] = "60";
            configuration["Authentication:PasswordReset:BaseUrl"] = "movieapp://reset-password";
            configuration["Authentication:RateLimit:LoginPermitLimit"] = "2";
            configuration["Authentication:RateLimit:LoginWindowMinutes"] = "1";
            configuration["Authentication:RateLimit:RegisterPermitLimit"] = "2";
            configuration["Authentication:RateLimit:RegisterWindowMinutes"] = "10";
            configuration["App:PublicBaseUrl"] = "https://api.test.local";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

internal static class ExceptionHandlingRateLimitIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_exception_handling_rate_limit_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}

[CollectionDefinition("ExceptionHandlingRateLimitApi")]
public sealed class ExceptionHandlingRateLimitApiTestsFixture : ICollectionFixture<ExceptionHandlingRateLimitApiFixture>;

public sealed class ExceptionHandlingRateLimitApiFixture : IAsyncLifetime
{
    public ExceptionHandlingRateLimitWebApplicationFactory Factory { get; } = new();

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
            .UseNpgsql(ExceptionHandlingRateLimitIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("ExceptionHandlingRateLimitApi")]
public sealed class ExceptionHandlingRateLimitApiTests(ExceptionHandlingRateLimitApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task RateLimitingRemains429WithProblemDetailsContract()
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

        Assert.Equal("application/problem+json", third.Content.Headers.ContentType?.MediaType);
        var payload = await third.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        var root = payload.RootElement;
        Assert.Equal(StatusCodes.Status429TooManyRequests, root.GetProperty("status").GetInt32());
        Assert.Equal("TOO_MANY_REQUESTS", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
    }
}
