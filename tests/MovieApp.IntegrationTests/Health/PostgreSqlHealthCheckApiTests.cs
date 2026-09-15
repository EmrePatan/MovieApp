using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MovieApp.IntegrationTests.Health;

public sealed class PostgreSqlHealthCheckWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string UnavailablePostgreSqlConnectionString =
        "Host=127.0.0.1;Port=54399;Database=movieapp_pg_health_tests;Username=movieapp;Password=integration-test-password";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            UnavailablePostgreSqlConnectionString);
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] = UnavailablePostgreSqlConnectionString;
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}

[CollectionDefinition("PostgreSqlHealthCheckApi")]
public sealed class PostgreSqlHealthCheckApiTestsFixture : ICollectionFixture<PostgreSqlHealthCheckApiFixture>;

public sealed class PostgreSqlHealthCheckApiFixture : IDisposable
{
    public PostgreSqlHealthCheckWebApplicationFactory Factory { get; } = new();

    public void Dispose() => Factory.Dispose();
}

[Collection("PostgreSqlHealthCheckApi")]
public sealed class PostgreSqlHealthCheckApiTests(PostgreSqlHealthCheckApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task ReadyReportsUnhealthyWhenConfiguredPostgreSqlIsUnavailable()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unhealthy", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("postgresql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("54399", body, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-test-password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
    }
}
