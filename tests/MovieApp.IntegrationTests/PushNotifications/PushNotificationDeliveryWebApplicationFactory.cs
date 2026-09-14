using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MovieApp.Application.Abstractions.PushNotifications;

namespace MovieApp.IntegrationTests.PushNotifications;

public sealed class PushNotificationDeliveryWebApplicationFactory : WebApplicationFactory<Program>
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
            configuration["PushNotifications:Enabled"] = "true";
            configuration["PushNotifications:MaxAttempts"] = "5";
            configuration["PushNotifications:DispatchBatchSize"] = "100";
            configuration["BackgroundJobs:Enabled"] = "false";
            configurationBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExpoPushClient>();
            services.AddSingleton<IExpoPushClient>(ExpoPushClient);
        });
    }
}
