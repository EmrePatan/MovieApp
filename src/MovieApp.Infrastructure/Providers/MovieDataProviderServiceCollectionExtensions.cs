using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

internal static class MovieDataProviderServiceCollectionExtensions
{
    internal static IServiceCollection AddMovieDataProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MovieProvidersOptions>()
            .Bind(configuration.GetSection(MovieProvidersOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<MovieProvidersOptions>, MovieProvidersOptionsValidator>();

        services.Configure<TmdbOptions>(configuration.GetSection($"{MovieProvidersOptions.SectionName}:Tmdb"));

        var movieProviders = configuration
            .GetSection(MovieProvidersOptions.SectionName)
            .Get<MovieProvidersOptions>() ?? new MovieProvidersOptions();

        services.AddSingleton<MovieDataProviderCallTracker>();
        services.AddSingleton<FakeTmdbMovieChangesProvider>();
        services.AddScoped<FakeTmdbMovieChangesProviderAdapter>();
        services.AddScoped<TmdbMovieChangesProvider>();
        services.AddScoped<FakeMovieDataProvider>();

        if (IsTmdbProvider(movieProviders.Provider))
        {
            services.AddOptions<TmdbOptions>()
                .Bind(configuration.GetSection($"{MovieProvidersOptions.SectionName}:Tmdb"))
                .Validate(
                    options => options.IsConfigured(),
                    "TMDB provider is selected but MovieProviders:Tmdb credentials are not configured.")
                .ValidateOnStart();

            services.AddHttpClient<TmdbApiClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<TmdbOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });

            services.AddScoped<TmdbMovieDataProvider>();
            services.AddScoped<TmdbLocalizedDetailDataProvider>();
            services.AddScoped<ILocalizedDetailDataProvider>(serviceProvider =>
                serviceProvider.GetRequiredService<TmdbLocalizedDetailDataProvider>());
        }
        else
        {
            services.AddScoped<ILocalizedDetailDataProvider, NullLocalizedDetailDataProvider>();
        }

        services.AddScoped<IMovieDataProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MovieProvidersOptions>>().Value;
            return ResolveMovieDataProvider(serviceProvider, options.Provider);
        });

        services.AddScoped<ITmdbMovieChangesProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MovieProvidersOptions>>().Value;
            return ResolveTmdbMovieChangesProvider(serviceProvider, options.Provider);
        });

        return services;
    }

    private static bool IsTmdbProvider(string provider) =>
        string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase);

    private static IMovieDataProvider ResolveMovieDataProvider(
        IServiceProvider serviceProvider,
        string providerName)
    {
        if (IsTmdbProvider(providerName))
        {
            return serviceProvider.GetRequiredService<TmdbMovieDataProvider>();
        }

        return serviceProvider.GetRequiredService<FakeMovieDataProvider>();
    }

    private static ITmdbMovieChangesProvider ResolveTmdbMovieChangesProvider(
        IServiceProvider serviceProvider,
        string providerName)
    {
        if (IsTmdbProvider(providerName))
        {
            return serviceProvider.GetRequiredService<TmdbMovieChangesProvider>();
        }

        return serviceProvider.GetRequiredService<FakeTmdbMovieChangesProviderAdapter>();
    }
}
