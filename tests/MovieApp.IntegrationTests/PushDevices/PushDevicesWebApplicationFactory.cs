using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MovieApp.IntegrationTests.PushDevices;

public sealed class PushDevicesWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            PushDevicesIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                PushDevicesIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["BackgroundJobs:Enabled"] = "false";
            configurationBuilder.AddInMemoryCollection(configuration);
        });
    }
}
