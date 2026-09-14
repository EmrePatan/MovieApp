using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieApp.Api.BackgroundJobs;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.IntegrationTests.PushNotifications;

namespace MovieApp.IntegrationTests.BackgroundJobs;

public sealed class BackgroundJobsWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestExpoPushClient ExpoPushClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        Environment.SetEnvironmentVariable(
            "PostgreSql__ConnectionString",
            PushNotificationDeliveryIntegrationDatabase.GetConnectionString());
        Environment.SetEnvironmentVariable(
            "Authentication__Jwt__SigningKey",
            IntegrationTestJwtSettings.SigningKey);

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = IntegrationTestJwtSettings.CreateConfiguration();
            configuration["PostgreSql:ConnectionString"] =
                PushNotificationDeliveryIntegrationDatabase.GetConnectionString();
            configuration["Redis:ConnectionString"] = string.Empty;
            configuration["Redis:InstanceName"] = "MovieApp:";
            configuration["MovieProviders:Provider"] = "Fake";
            configuration["BackgroundJobs:Enabled"] = "false";
            configuration["PushNotifications:Enabled"] = "true";
            configurationBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExpoPushClient>();
            services.AddSingleton<IExpoPushClient>(ExpoPushClient);
            services.AddScoped<PushDeliveryPreparationJob>();
        });
    }
}
