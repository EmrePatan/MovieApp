using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Reviews;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

public sealed class AzureReviewTranslationProvider(
    AzureTranslatorApiClient apiClient,
    IOptions<AzureTranslatorOptions> options,
    ILogger<AzureReviewTranslationProvider> logger) : IReviewTranslationProvider
{
    public async Task<ReviewTranslationProviderResult> TranslateAsync(
        string text,
        string targetContentLocale,
        CancellationToken cancellationToken = default)
    {
        var translatorOptions = options.Value;
        if (!translatorOptions.IsConfigured())
        {
            throw new ReviewTranslationProviderException("Azure Translator is not configured.");
        }

        var targetLanguage = TranslatorLanguageCodes.FromContentLocale(targetContentLocale);
        try
        {
            var items = await apiClient.TranslateAsync(
                [text],
                targetLanguage,
                sourceLanguage: null,
                cancellationToken);
            if (items.Count == 0)
            {
                throw new ReviewTranslationProviderException("Azure Translator returned an invalid response.");
            }

            var item = items[0];
            if (item is null)
            {
                throw new ReviewTranslationProviderException("Azure Translator returned an invalid response.");
            }

            var detectedLanguage = item.DetectedSourceLanguage;
            if (string.IsNullOrWhiteSpace(detectedLanguage))
            {
                throw new ReviewTranslationProviderException("Azure Translator returned an invalid response.");
            }

            var sourceMatchesTarget = TranslatorLanguageCodes.SourceMatchesTarget(
                detectedLanguage,
                targetContentLocale);

            return new ReviewTranslationProviderResult(
                sourceMatchesTarget ? null : item.Text,
                detectedLanguage,
                sourceMatchesTarget);
        }
        catch (ReviewTranslationQuotaExceededException)
        {
            throw;
        }
        catch (ReviewTranslationProviderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AzureReviewTranslationProviderLogMessages.LogProviderFailure(logger, 0);
            throw new ReviewTranslationProviderException("Azure Translator request failed.", exception);
        }
    }
}
