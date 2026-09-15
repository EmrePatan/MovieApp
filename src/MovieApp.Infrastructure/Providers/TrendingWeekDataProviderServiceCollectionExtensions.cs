using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

internal static class TrendingWeekDataProviderServiceCollectionExtensions
{
    internal static IServiceCollection AddTrendingWeekDataProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var movieProviders = configuration
            .GetSection(MovieProvidersOptions.SectionName)
            .Get<MovieProvidersOptions>() ?? new MovieProvidersOptions();

        services.AddSingleton<FakeTrendingWeekDataProvider>();

        if (IsTmdbProvider(movieProviders.Provider))
        {
            services.AddScoped<TmdbTrendingWeekDataProvider>();
        }

        services.AddScoped<ITrendingWeekDataProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MovieProvidersOptions>>().Value;
            return IsTmdbProvider(options.Provider)
                ? serviceProvider.GetRequiredService<TmdbTrendingWeekDataProvider>()
                : serviceProvider.GetRequiredService<FakeTrendingWeekDataProvider>();
        });

        return services;
    }

    private static bool IsTmdbProvider(string provider) =>
        string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase);
}
