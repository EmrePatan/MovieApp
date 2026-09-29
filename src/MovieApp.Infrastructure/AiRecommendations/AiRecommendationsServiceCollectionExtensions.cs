using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.AiRecommendations;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.AiRecommendations;

public static class AiRecommendationsServiceCollectionExtensions
{
    public static IServiceCollection AddAiRecommendations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AiRecommendationOptions>()
            .Bind(configuration.GetSection(AiRecommendationOptions.SectionName));

        var movieProviders = configuration
            .GetSection(MovieProvidersOptions.SectionName)
            .Get<MovieProvidersOptions>() ?? new MovieProvidersOptions();

        services.AddScoped<IAiRecommendationPerfContext, AiRecommendationPerfContext>();
        services.AddScoped<IAiTasteProfileDataSource, AiTasteProfileDataSource>();
        services.AddScoped<IAiTasteProfileBuilder, AiTasteProfileBuilder>();
        if (IsTmdbProvider(movieProviders.Provider))
        {
            services.AddScoped<IAiRecommendationTmdbSearch, AiRecommendationTmdbSearch>();
        }
        else
        {
            services.AddScoped<IAiRecommendationTmdbSearch, NullAiRecommendationTmdbSearch>();
        }
        services.AddScoped<IAiRequestedPersonFilmographyResolver, AiRequestedPersonFilmographyResolver>();
        services.AddScoped<IMovieIdentityResolver, AiMovieIdentityResolver>();
        services.AddScoped<IAiMovieRecommendationValidator, AiMovieRecommendationValidator>();
        services.AddScoped<IAiMovieRecommendationService, AiMovieRecommendationService>();
        services.AddScoped<IDeterministicAiMovieRecommendationProvider, DeterministicAiMovieRecommendationProvider>();
        services.AddSingleton<IAiRecommendationSessionStore, RedisAiRecommendationSessionStore>();
        services.AddSingleton<IAiRecommendationQuotaService, AiRecommendationQuotaService>();
        services.AddSingleton<IAiRecommendationEntitlementService, AiRecommendationEntitlementService>();

        RegisterExternalProviderHttpClient<GeminiAiMovieRecommendationProvider>(
            services,
            (options, client) => client.Timeout = TimeSpan.FromSeconds(
                Math.Max(options.ExternalLlmChainBudgetSeconds + 2, options.ResolveProviderTimeoutSeconds(options.Gemini.RequestTimeoutSeconds) + 2)));

        RegisterExternalProviderHttpClient<GroqAiMovieRecommendationProvider>(
            services,
            (options, client) => client.Timeout = TimeSpan.FromSeconds(
                Math.Max(options.ExternalLlmChainBudgetSeconds + 2, options.ResolveProviderTimeoutSeconds(options.Groq.RequestTimeoutSeconds) + 2)));

        RegisterExternalProviderHttpClient<OpenRouterAiMovieRecommendationProvider>(
            services,
            (options, client) => client.Timeout = TimeSpan.FromSeconds(
                Math.Max(options.ExternalLlmChainBudgetSeconds + 2, options.ResolveProviderTimeoutSeconds(options.OpenRouter.RequestTimeoutSeconds) + 2)));

        RegisterExternalProviderHttpClient<CloudflareWorkersAiMovieRecommendationProvider>(
            services,
            (options, client) => client.Timeout = TimeSpan.FromSeconds(
                Math.Max(options.ExternalLlmChainBudgetSeconds + 2, options.ResolveProviderTimeoutSeconds(options.Cloudflare.RequestTimeoutSeconds) + 2)));

        services.AddTransient<IAiExternalLlmRecommendationProvider, GeminiAiMovieRecommendationProvider>();
        services.AddTransient<IAiExternalLlmRecommendationProvider, GroqAiMovieRecommendationProvider>();
        services.AddTransient<IAiExternalLlmRecommendationProvider, OpenRouterAiMovieRecommendationProvider>();
        services.AddTransient<IAiExternalLlmRecommendationProvider, CloudflareWorkersAiMovieRecommendationProvider>();

        services.AddScoped<FailoverAiMovieRecommendationProvider>();
        services.AddScoped<IAiMovieRecommendationProvider>(provider =>
            provider.GetRequiredService<FailoverAiMovieRecommendationProvider>());

        return services;
    }

    private static bool IsTmdbProvider(string provider) =>
        string.Equals(provider, MovieDataProviderNames.Tmdb, StringComparison.OrdinalIgnoreCase);

    private static void RegisterExternalProviderHttpClient<TProvider>(
        IServiceCollection services,
        Action<AiRecommendationOptions, HttpClient> configureClient)
        where TProvider : class
    {
        services.AddHttpClient<TProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiRecommendationOptions>>().Value;
            configureClient(options, client);
        });
    }
}
