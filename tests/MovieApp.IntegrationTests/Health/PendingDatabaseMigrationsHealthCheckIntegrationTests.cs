using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Health;

[CollectionDefinition("PendingDatabaseMigrationsHealthCheckApi")]
public sealed class PendingDatabaseMigrationsHealthCheckApiTestsFixtureDefinition : ICollectionFixture<PendingDatabaseMigrationsHealthCheckApiFixture>;

public sealed class PendingDatabaseMigrationsHealthCheckApiFixture : IAsyncLifetime
{
    private const string DatabaseName = "movieapp_pending_migrations_health_tests";

    public PendingDatabaseMigrationsHealthCheckWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    internal static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString(DatabaseName))
            .Options;

        return new ApplicationDbContext(options);
    }

    public sealed class PendingDatabaseMigrationsHealthCheckWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            Environment.SetEnvironmentVariable(
                "PostgreSql__ConnectionString",
                IntegrationTestDatabase.GetConnectionString(DatabaseName));
            Environment.SetEnvironmentVariable(
                "Authentication__Jwt__SigningKey",
                IntegrationTestJwtSettings.SigningKey);

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                var configuration = IntegrationTestJwtSettings.CreateConfiguration();
                configuration["PostgreSql:ConnectionString"] =
                    IntegrationTestDatabase.GetConnectionString(DatabaseName);
                configuration["Redis:ConnectionString"] = string.Empty;
                configuration["Redis:InstanceName"] = "MovieApp:";
                configuration["MovieProviders:Provider"] = "Fake";
                configurationBuilder.AddInMemoryCollection(configuration);
            });
        }
    }
}

[Collection("PendingDatabaseMigrationsHealthCheckApi")]
public sealed class PendingDatabaseMigrationsHealthCheckIntegrationTests(
    PendingDatabaseMigrationsHealthCheckApiFixture fixture)
{
    [Fact]
    public async Task ReadyReportsUnhealthyWhenPendingMigrationsExist()
    {
        try
        {
            await using (var context = PendingDatabaseMigrationsHealthCheckApiFixture.CreateContext())
            {
                await context.Database.ExecuteSqlRawAsync(
                    """
                    DELETE FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20260919141753_AddDataProtectionKeys';
                    """);

                await context.Database.ExecuteSqlRawAsync(
                    """
                    DROP TABLE IF EXISTS "DataProtectionKeys";
                    """);
            }

            var client = fixture.Factory.CreateClient();
            var response = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("database-migrations", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Unhealthy", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Check failed.", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await using var context = PendingDatabaseMigrationsHealthCheckApiFixture.CreateContext();
            await context.Database.MigrateAsync();
        }
    }

    [Fact]
    public async Task ReadyReportsHealthyWhenDatabaseSchemaIsCurrent()
    {
        var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("database-migrations", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }
}
