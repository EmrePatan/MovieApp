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
        services.AddScoped<FakePersonDataProvider>();
        services.AddScoped<FakeWatchProviderService>();
        services.AddScoped<FakeVideoProvider>();

        if (IsTmdbProvider(movieProviders.Provider))
        {
            services.AddScoped<TmdbCreditsProvider>();
            services.AddScoped<TmdbPersonDataProvider>();
            services.AddScoped<TmdbWatchProviderService>();
            services.AddScoped<TmdbVideoProvider>();
        }

        services.AddScoped<ICreditsProvider>(serviceProvider =>
            IsTmdbProvider(movieProviders.Provider)
                ? serviceProvider.GetRequiredService<TmdbCreditsProvider>()
                : serviceProvider.GetRequiredService<FakeCreditsProvider>());

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

        return services;
    }

    private static bool IsTmdbProvider(string provider) =>
        string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase);
}
