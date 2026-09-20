using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Contracts.Health;
using MovieApp.IntegrationTests.Errors;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Observability;

public sealed class ProductionObservabilityWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            ProductionObservabilityIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureServices(services =>
        {
            services.AddControllers()
                .AddApplicationPart(typeof(ExceptionHandlingTestController).Assembly);
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                ProductionObservabilityIntegrationDatabase.GetConnectionString();
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

internal static class ProductionObservabilityIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_production_observability_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}

[CollectionDefinition("ProductionObservabilityApi")]
public sealed class ProductionObservabilityApiTestsFixture : ICollectionFixture<ProductionObservabilityApiFixture>;

public sealed class ProductionObservabilityApiFixture : IAsyncLifetime
{
    public ProductionObservabilityWebApplicationFactory Factory { get; } = new();

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
            .UseNpgsql(ProductionObservabilityIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("ProductionObservabilityApi")]
public sealed class ProductionObservabilityApiTests(ProductionObservabilityApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task LiveEndpointSucceedsWithoutExternalProviders()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
    }

    [Fact]
    public async Task ReadyEndpointChecksConfiguredDependencies()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("postgresql", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceVersion", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("environment", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadyResponseDoesNotLeakSecrets()
    {
        var response = await _client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SigningKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorrelationIdGeneratedWhenMissing()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        var correlationId = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Equal(32, correlationId.Length);
    }

    [Fact]
    public async Task ValidSuppliedCorrelationIdIsEchoed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "integration-correlation-001");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("integration-correlation-001", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task MalformedCorrelationIdIsReplacedSafely()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", new string('x', 200));

        var response = await _client.SendAsync(request);

        var correlationId = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Equal(32, correlationId.Length);
        Assert.NotEqual(new string('x', 200), correlationId);
    }

    [Fact]
    public async Task ErrorResponseContainsSafeCorrelationReference()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/errors/unhandled");
        request.Headers.Add("X-Correlation-Id", "error-correlation-001");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error-correlation-001", payload.RootElement.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ErrorResponseDoesNotExposeStackTrace()
    {
        var response = await _client.GetAsync("/__test/errors/unhandled");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("at ", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HangfireDashboardIsNotPubliclyExposed()
    {
        var response = await _client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
