using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Api.BackgroundJobs;

namespace MovieApp.IntegrationTests.ReleaseNotifications;

public sealed class ReleaseNotificationFanoutWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            ReleaseNotificationFanoutIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                ReleaseNotificationFanoutIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["BackgroundJobs:Enabled"] = "false";
            configurationBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.AddScoped<ReleaseNotificationFanoutJob>();
        });
    }
}
