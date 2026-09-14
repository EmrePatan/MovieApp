using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MovieApp.IntegrationTests.TvShowCatalogSyncState;

public sealed class TvShowCatalogSyncStateWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            TvShowCatalogSyncStateIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                TvShowCatalogSyncStateIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}
