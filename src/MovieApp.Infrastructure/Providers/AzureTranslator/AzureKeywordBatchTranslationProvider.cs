using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

public sealed class AzureKeywordBatchTranslationProvider(
    AzureTranslatorApiClient apiClient) : IKeywordBatchTranslationProvider
{
    public async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> sourceTexts,
        string targetContentLocale,
        CancellationToken cancellationToken = default)
    {
        if (sourceTexts.Count == 0)
        {
            return [];
        }

        var targetLanguage = TranslatorLanguageCodes.FromContentLocale(targetContentLocale);
        try
        {
            var items = await apiClient.TranslateAsync(
                sourceTexts,
                targetLanguage,
                sourceLanguage: "en",
                cancellationToken);

            if (items.Count != sourceTexts.Count)
            {
                throw new KeywordTranslationProviderException("Azure Translator batch response count mismatch.");
            }

            return items.Select(item => item.Text).ToList();
        }
        catch (ReviewTranslationQuotaExceededException)
        {
            throw new KeywordTranslationQuotaExceededException();
        }
        catch (ReviewTranslationProviderException exception)
        {
            throw new KeywordTranslationProviderException(exception.Message, exception);
        }
    }
}
