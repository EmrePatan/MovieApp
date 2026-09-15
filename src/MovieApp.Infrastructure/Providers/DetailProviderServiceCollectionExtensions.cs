using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

internal static class DetailProviderServiceCollectionExtensions
{
    internal static IServiceCollection AddDetailProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var movieProviders = configuration
            .GetSection(MovieProvidersOptions.SectionName)
            .Get<MovieProvidersOptions>() ?? new MovieProvidersOptions();

        services.AddScoped<FakeCreditsProvider>();
        services.AddScoped<FakeKeywordsProvider>();
        services.AddScoped<FakePersonDataProvider>();
        services.AddScoped<FakeWatchProviderService>();
        services.AddScoped<FakeVideoProvider>();
        services.AddScoped<FakeImageProvider>();
        services.AddScoped<FakeCollectionDataProvider>();
        services.AddScoped<FakeMovieReleaseDatesProvider>();
        services.AddScoped<FakeDiscoveryWatchProviderCatalog>();

        if (IsTmdbProvider(movieProviders.Provider))
        {
            services.AddScoped<TmdbCreditsProvider>();
            services.AddScoped<TmdbKeywordsProvider>();
            services.AddScoped<TmdbPersonDataProvider>();
            services.AddScoped<TmdbWatchProviderService>();
            services.AddScoped<TmdbVideoProvider>();
            services.AddScoped<TmdbImageProvider>();
            services.AddScoped<TmdbCollectionDataProvider>();
            services.AddScoped<TmdbMovieReleaseDatesProvider>();
            services.AddScoped<TmdbDiscoveryWatchProviderCatalog>();
        }

        services.AddScoped<ICreditsProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbCreditsProvider>()
                : serviceProvider.GetRequiredService<FakeCreditsProvider>());

        services.AddScoped<IKeywordsProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbKeywordsProvider>()
                : serviceProvider.GetRequiredService<FakeKeywordsProvider>());

        services.AddScoped<IWatchProviderService>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbWatchProviderService>()
                : serviceProvider.GetRequiredService<FakeWatchProviderService>());

        services.AddScoped<IPersonDataProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbPersonDataProvider>()
                : serviceProvider.GetRequiredService<FakePersonDataProvider>());

        services.AddScoped<IVideoProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbVideoProvider>()
                : serviceProvider.GetRequiredService<FakeVideoProvider>());

        services.AddScoped<IImageProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbImageProvider>()
                : serviceProvider.GetRequiredService<FakeImageProvider>());

        services.AddScoped<ICollectionDataProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbCollectionDataProvider>()
                : serviceProvider.GetRequiredService<FakeCollectionDataProvider>());

        services.AddScoped<IMovieReleaseDatesProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbMovieReleaseDatesProvider>()
                : serviceProvider.GetRequiredService<FakeMovieReleaseDatesProvider>());

        services.AddScoped<IDiscoveryWatchProviderCatalog>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbDiscoveryWatchProviderCatalog>()
                : serviceProvider.GetRequiredService<FakeDiscoveryWatchProviderCatalog>());

        return services;
    }

    private static bool IsTmdbProvider(string provider) =>
        string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase);
}
