using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Contracts.Auth;
using MovieApp.Contracts.Health;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Errors;

public sealed class ExceptionHandlingWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            ExceptionHandlingIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);
        Environment.SetEnvironmentVariable("Authentication__RateLimit__RegisterPermitLimit", "100");
        Environment.SetEnvironmentVariable("Authentication__RateLimit__RegisterWindowMinutes", "10");

        builder.ConfigureServices(services =>
        {
            services.AddControllers()
                .AddApplicationPart(typeof(ExceptionHandlingTestController).Assembly);
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                ExceptionHandlingIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["Authentication:PasswordReset:TokenLifetimeMinutes"] = "60";
            configuration["Authentication:PasswordReset:BaseUrl"] = "movieapp://reset-password";
            configuration["Authentication:RateLimit:RegisterPermitLimit"] = "100";
            configuration["Authentication:RateLimit:RegisterWindowMinutes"] = "10";
            configuration["App:PublicBaseUrl"] = "https://api.test.local";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

internal static class ExceptionHandlingIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_exception_handling_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}

[CollectionDefinition("ExceptionHandlingApi")]
public sealed class ExceptionHandlingApiTestsFixture : ICollectionFixture<ExceptionHandlingApiFixture>;

public sealed class ExceptionHandlingApiFixture : IAsyncLifetime
{
    public ExceptionHandlingWebApplicationFactory Factory { get; } = new();

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
            .UseNpgsql(ExceptionHandlingIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("ExceptionHandlingApi")]
public sealed class GlobalExceptionHandlingApiTests(ExceptionHandlingApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UnhandledExceptionReturns500WithSafeProblemDetails()
    {
        var response = await _client.GetAsync("/__test/errors/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertProblemDetails(
            response,
            StatusCodes.Status500InternalServerError,
            "INTERNAL_ERROR",
            mustNotContain: ["SensitiveConnectionString", "InvalidOperationException", "at "]);
    }

    [Fact]
    public async Task NotFoundBehaviorRemains404()
    {
        var response = await _client.GetAsync($"/api/movies/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response, StatusCodes.Status404NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task ValidationErrorRemains400AndUseful()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "not-an-email",
            "short",
            ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await ReadProblemDetailsAsync(response);
        Assert.Equal(StatusCodes.Status400BadRequest, payload.GetProperty("status").GetInt32());
        Assert.Equal("VALIDATION_FAILED", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task UnauthorizedRemains401()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConflictRemains409()
    {
        var email = $"conflict-{Guid.NewGuid():N}@example.com";
        var first = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Conflict User"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "StrongPassword123",
            "Another User"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await AssertProblemDetailsAsync(duplicate, StatusCodes.Status409Conflict, "CONFLICT");
    }

    [Fact]
    public async Task HealthEndpointsRemainFunctional()
    {
        var health = await _client.GetAsync("/health");
        var ready = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var healthPayload = await health.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(healthPayload);
        Assert.Equal("Healthy", healthPayload.Status);
    }

    [Fact]
    public async Task ControllerProblemDetailsIncludeTraceIdAndCode()
    {
        var response = await _client.GetAsync($"/api/movies/{Guid.NewGuid():D}");
        await AssertProblemDetailsAsync(response, StatusCodes.Status404NotFound, "NOT_FOUND");
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        int expectedStatus,
        string expectedCode)
    {
        AssertProblemDetails(response, expectedStatus, expectedCode);
        await ReadProblemDetailsAsync(response);
    }

    private static void AssertProblemDetails(
        HttpResponseMessage response,
        int expectedStatus,
        string expectedCode,
        string[]? mustNotContain = null)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var payload = ReadProblemDetailsAsync(response).GetAwaiter().GetResult();
        Assert.Equal(expectedStatus, payload.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));

        if (mustNotContain is not null)
        {
            var body = payload.GetRawText();
            foreach (var forbidden in mustNotContain)
            {
                Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal);
            }
        }
    }

    private static async Task<JsonElement> ReadProblemDetailsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }
}
