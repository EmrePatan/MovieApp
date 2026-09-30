using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

public static class KeywordTranslationServiceCollectionExtensions
{
    public static IServiceCollection AddKeywordTranslation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<KeywordLocalizationBackfillOptions>()
            .Bind(configuration.GetSection(KeywordLocalizationBackfillOptions.SectionName));

        services.AddScoped<IKeywordBatchTranslationProvider, AzureKeywordBatchTranslationProvider>();

        return services;
    }
}
