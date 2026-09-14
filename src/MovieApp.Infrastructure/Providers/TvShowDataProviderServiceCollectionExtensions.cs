using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

internal static class TvShowDataProviderServiceCollectionExtensions
{
    internal static IServiceCollection AddTvShowDataProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var movieProviders = configuration
            .GetSection(MovieProvidersOptions.SectionName)
            .Get<MovieProvidersOptions>() ?? new MovieProvidersOptions();

        services.AddSingleton<TvShowDataProviderCallTracker>();
        services.AddSingleton<FakeTmdbTvChangesProvider>();
        services.AddScoped<FakeTmdbTvChangesProviderAdapter>();
        services.AddScoped<TmdbTvChangesProvider>();
        services.AddScoped<FakeTvShowDataProvider>();

        if (IsTmdbProvider(movieProviders.Provider))
        {
            services.AddScoped<TmdbTvShowDataProvider>();
            services.AddScoped<ITvShowExternalIdResolver, TmdbTvExternalIdResolver>();
        }
        else
        {
            services.AddScoped<ITvShowExternalIdResolver, FakeTvExternalIdResolver>();
        }

        services.AddScoped<ITvShowDataProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MovieProvidersOptions>>().Value;
            return ResolveTvShowDataProvider(serviceProvider, options.Provider);
        });

        services.AddScoped<ITmdbTvChangesProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MovieProvidersOptions>>().Value;
            return ResolveTmdbTvChangesProvider(serviceProvider, options.Provider);
        });

        return services;
    }

    private static bool IsTmdbProvider(string provider) =>
        string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase);

    private static ITvShowDataProvider ResolveTvShowDataProvider(
        IServiceProvider serviceProvider,
        string providerName)
    {
        if (IsTmdbProvider(providerName))
        {
            return serviceProvider.GetRequiredService<TmdbTvShowDataProvider>();
        }

        return serviceProvider.GetRequiredService<FakeTvShowDataProvider>();
    }

    private static ITmdbTvChangesProvider ResolveTmdbTvChangesProvider(
        IServiceProvider serviceProvider,
        string providerName)
    {
        if (IsTmdbProvider(providerName))
        {
            return serviceProvider.GetRequiredService<TmdbTvChangesProvider>();
        }

        return serviceProvider.GetRequiredService<FakeTmdbTvChangesProviderAdapter>();
    }
}
