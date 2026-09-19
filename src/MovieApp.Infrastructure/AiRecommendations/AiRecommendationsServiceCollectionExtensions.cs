using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

public static class AiRecommendationsServiceCollectionExtensions
{
    public static IServiceCollection AddAiRecommendations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AiRecommendationOptions>()
            .Bind(configuration.GetSection(AiRecommendationOptions.SectionName));

        services.AddScoped<IAiRecommendationPerfContext, AiRecommendationPerfContext>();
        services.AddScoped<IAiTasteProfileDataSource, AiTasteProfileDataSource>();
        services.AddScoped<IAiTasteProfileBuilder, AiTasteProfileBuilder>();
        services.AddScoped<IMovieIdentityResolver, AiMovieIdentityResolver>();
        services.AddScoped<IAiMovieRecommendationValidator, AiMovieRecommendationValidator>();
        services.AddScoped<IAiMovieRecommendationService, AiMovieRecommendationService>();
        services.AddSingleton<IAiRecommendationSessionStore, RedisAiRecommendationSessionStore>();
        services.AddSingleton<IAiRecommendationQuotaService, AiRecommendationQuotaService>();
        services.AddSingleton<IAiRecommendationEntitlementService, AiRecommendationEntitlementService>();

        services.AddHttpClient<IAiMovieRecommendationProvider, GeminiAiMovieRecommendationProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiRecommendationOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.Gemini.RequestTimeoutSeconds));
        });

        return services;
    }
}
