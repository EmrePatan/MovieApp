using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.ExternalRatings;

namespace MovieApp.Infrastructure.Providers.MdbList;

internal static class ExternalRatingsProviderServiceCollectionExtensions
{
    internal static IServiceCollection AddExternalRatingsProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ExternalRatingsOptions>()
            .Bind(configuration.GetSection(ExternalRatingsOptions.SectionName));

        services.AddOptions<MdbListOptions>()
            .Bind(configuration.GetSection(MdbListOptions.SectionName));

        services.AddSingleton<IExternalRatingsFeatureState, ExternalRatingsFeatureState>();
        services.AddSingleton<IExternalRatingsRefreshCoalescer, ExternalRatingsRefreshCoalescer>();

        services.AddHttpClient<MdbListApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MdbListOptions>>().Value;
            var baseUrl = options.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });

        services.AddScoped<MdbListExternalRatingsProvider>();
        services.AddScoped<NullExternalRatingsProvider>();
        services.AddScoped<IExternalRatingsProvider>(serviceProvider =>
        {
            var featureState = serviceProvider.GetRequiredService<IExternalRatingsFeatureState>();
            return featureState.IsOperational
                ? serviceProvider.GetRequiredService<MdbListExternalRatingsProvider>()
                : serviceProvider.GetRequiredService<NullExternalRatingsProvider>();
        });

        return services;
    }
}
