using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieApp.Application;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Services.Home;
using Microsoft.Extensions.Options;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;
using MovieApp.Infrastructure;

namespace MovieApp.HomeStampedeBenchmark;

internal static class BenchmarkHost
{
    public static ServiceProvider CreateServiceProvider(Guid userId)
    {
        var apiDevelopmentSettings = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MovieApp.Api", "appsettings.Development.json"));

        var repoRootEnv = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"));

        var configurationBuilder = new ConfigurationBuilder()
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
        services.AddScoped<ICurrentUser>(_ => new BenchmarkCurrentUser(userId));
        return services.BuildServiceProvider();
    }

    public static async Task<string> ResolveHomeCacheKeyAsync(
        IServiceProvider provider,
        Guid userId,
        string contentLocale,
        string? releaseRegion)
    {
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var releaseOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ReleaseRegionOptions>>().Value;
        var region = string.IsNullOrWhiteSpace(releaseRegion)
            ? WatchProviderRegionValidator.Normalize(releaseOptions.DefaultRegion)
            : WatchProviderRegionValidator.Normalize(releaseRegion);
        var generation = await new UserRecommendationCacheGeneration(cache).GetAsync(userId);
        return HomeCacheKeys.Create(
            userId,
            SearchContentType.All,
            SectionSize,
            contentLocale,
            region,
            generation);
    }

    public static async Task<string> ResolveRecommendationHomeCacheKeyAsync(
        IServiceProvider provider,
        Guid userId,
        string contentLocale)
    {
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var homeOptions = scope.ServiceProvider.GetRequiredService<IOptions<HomeOptions>>().Value;
        var recommendationOptions = scope.ServiceProvider.GetRequiredService<IOptions<RecommendationOptions>>().Value;
        var generation = await new UserRecommendationCacheGeneration(cache).GetAsync(userId);
        var recommendationLimit = Math.Max(
            SectionSize + Math.Max(0, homeOptions.HeroSectionSize) + Math.Max(0, recommendationOptions.HomeRecommendationSurplus),
            80);
        return RecommendationCacheKeys.Home(
            userId,
            contentLocale,
            generation,
            RecommendationContentType.All,
            recommendationLimit,
            diversified: false,
            includeBecauseYouWatched: false);
    }

    public const int SectionSize = 10;

    private sealed class BenchmarkHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "HomeStampedeBenchmark";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    private sealed class BenchmarkCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;
    }
}
