using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Health;

public sealed class RedisHealthCheckWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            RedisHealthCheckIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);
        Environment.SetEnvironmentVariable("Redis__ConnectionString", "127.0.0.1:6399");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                RedisHealthCheckIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = "127.0.0.1:6399";
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

internal static class RedisHealthCheckIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_redis_health_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}

[CollectionDefinition("RedisHealthCheckApi")]
public sealed class RedisHealthCheckApiTestsFixture : ICollectionFixture<RedisHealthCheckApiFixture>;

public sealed class RedisHealthCheckApiFixture : IAsyncLifetime
{
    public RedisHealthCheckWebApplicationFactory Factory { get; } = new();

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
            .UseNpgsql(RedisHealthCheckIntegrationDatabase.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }
}

[Collection("RedisHealthCheckApi")]
public sealed class RedisHealthCheckApiTests(RedisHealthCheckApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task ReadyReportsUnhealthyWhenConfiguredRedisIsUnavailable()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unhealthy", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("6399", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
    }
}
