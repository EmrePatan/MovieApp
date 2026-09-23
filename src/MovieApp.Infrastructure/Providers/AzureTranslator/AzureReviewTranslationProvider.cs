using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Reviews;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

public sealed class AzureReviewTranslationProvider(
    HttpClient httpClient,
    IOptions<AzureTranslatorOptions> options,
    ILogger<AzureReviewTranslationProvider> logger) : IReviewTranslationProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
        var requestUri = BuildTranslateUri(translatorOptions.Endpoint, targetLanguage);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("Ocp-Apim-Subscription-Key", translatorOptions.SubscriptionKey);
        request.Headers.Add("Ocp-Apim-Subscription-Region", translatorOptions.Region);
        request.Content = JsonContent.Create(
            new[] { new AzureTranslateRequest(text) },
            options: SerializerOptions);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReviewTranslationProviderException("Azure Translator request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ReviewTranslationProviderException("Azure Translator request failed.", exception);
        }

        using (response)
        {

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ReviewTranslationQuotaExceededException();
            }

            if (!response.IsSuccessStatusCode)
            {
                AzureReviewTranslationProviderLogMessages.LogProviderFailure(
                    logger,
                    (int)response.StatusCode);
                throw new ReviewTranslationProviderException(
                    $"Azure Translator returned status code {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<AzureTranslateResponseItem[]>(
                SerializerOptions,
                cancellationToken);

            var item = payload is { Length: > 0 } ? payload[0] : null;
            var translation = item?.Translations is { Count: > 0 } translations
                ? translations[0]
                : null;
            var detectedLanguage = item?.DetectedLanguage?.Language;

            if (translation is null || string.IsNullOrWhiteSpace(detectedLanguage))
            {
                throw new ReviewTranslationProviderException("Azure Translator returned an invalid response.");
            }

            var sourceMatchesTarget = TranslatorLanguageCodes.SourceMatchesTarget(
                detectedLanguage,
                targetContentLocale);

            return new ReviewTranslationProviderResult(
                sourceMatchesTarget ? null : translation.Text,
                detectedLanguage,
                sourceMatchesTarget);
        }
    }

    private static string BuildTranslateUri(string endpoint, string targetLanguage)
    {
        var baseEndpoint = endpoint.TrimEnd('/');
        return $"{baseEndpoint}/translate?api-version=3.0&to={Uri.EscapeDataString(targetLanguage)}";
    }

    private sealed record AzureTranslateRequest(string Text);

    private sealed class AzureTranslateResponseItem
    {
        public AzureDetectedLanguage? DetectedLanguage { get; init; }

        public IReadOnlyList<AzureTranslation>? Translations { get; init; }
    }

    private sealed class AzureDetectedLanguage
    {
        public string Language { get; init; } = string.Empty;
    }

    private sealed class AzureTranslation
    {
        public string Text { get; init; } = string.Empty;
    }
}
