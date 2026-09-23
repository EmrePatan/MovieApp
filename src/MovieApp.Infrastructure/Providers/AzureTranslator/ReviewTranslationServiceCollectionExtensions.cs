using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Reviews;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

public static class ReviewTranslationServiceCollectionExtensions
{
    public static IServiceCollection AddReviewTranslation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AzureTranslatorOptions>()
            .Bind(configuration.GetSection(AzureTranslatorOptions.SectionName));

        services.AddHttpClient<IReviewTranslationProvider, AzureReviewTranslationProvider>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureTranslatorOptions>>()
                .Value;

            client.Timeout = TimeSpan.FromSeconds(Math.Max(3, options.RequestTimeoutSeconds));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
