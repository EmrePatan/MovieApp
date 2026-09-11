using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Providers;

namespace MovieApp.Infrastructure.Providers;

internal static class TvShowDataProviderServiceCollectionExtensions
{
    internal static IServiceCollection AddTvShowDataProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<TvShowDataProviderCallTracker>();
        services.AddScoped<FakeTvShowDataProvider>();
        services.AddScoped<ITvShowExternalIdResolver, FakeTvExternalIdResolver>();

        // Only Fake is implemented for TV shows in this step.
        services.AddScoped<ITvShowDataProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeTvShowDataProvider>());

        return services;
    }
}
