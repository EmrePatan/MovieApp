using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application;
using MovieApp.Infrastructure;

namespace MovieApp.KeywordLocalizationOps;

internal static class KeywordLocalizationOpsHost
{
    public static ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(typeof(KeywordLocalizationOpsHost).Assembly)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
