using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieApp.Application;
using MovieApp.Infrastructure;

namespace MovieApp.LocalizationPerfBenchmark;

internal static class BenchmarkHost
{
    public static ServiceProvider CreateServiceProvider()
    {
        var apiDevelopmentSettings = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "MovieApp.Api", "appsettings.Development.json"));

        var repoRootEnv = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"));

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(apiDevelopmentSettings, optional: true);
        DotEnvConfigurationExtensions.AddDotEnvFile(configurationBuilder, repoRootEnv);
        var configuration = configurationBuilder
            .AddUserSecrets<BenchmarkHostEnvironment>()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new BenchmarkHostEnvironment());
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options => options.SingleLine = true);
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class BenchmarkHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "LocalizationPerfBenchmark";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
