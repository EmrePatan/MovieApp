using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MovieApp.Application.Exceptions;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Providers.AzureTranslator;

public sealed class AzureTranslatorApiClient(
    HttpClient httpClient,
    IOptions<AzureTranslatorOptions> options)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<AzureTranslatorTranslationItem>> TranslateAsync(
        IReadOnlyList<string> texts,
        string targetLanguage,
        string? sourceLanguage,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var translatorOptions = options.Value;
        if (!translatorOptions.IsConfigured())
        {
            throw new ReviewTranslationProviderException("Azure Translator is not configured.");
        }

        var requestUri = BuildTranslateUri(translatorOptions.Endpoint, targetLanguage, sourceLanguage);
        var payload = texts.Select(text => new AzureTranslateRequest(text)).ToArray();

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", translatorOptions.SubscriptionKey);
            request.Headers.Add("Ocp-Apim-Subscription-Region", translatorOptions.Region);
            request.Content = JsonContent.Create(payload, options: SerializerOptions);

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
                if (attempt == maxAttempts)
                {
                    throw new ReviewTranslationProviderException("Azure Translator request failed.", exception);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt == maxAttempts)
                    {
                        throw new ReviewTranslationQuotaExceededException();
                    }

                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 * attempt);
                    await Task.Delay(retryAfter, cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode is 401 or 403)
                {
                    throw new ReviewTranslationProviderException(
                        $"Azure Translator authentication failed with status {(int)response.StatusCode}.");
                }

                if ((int)response.StatusCode >= 500)
                {
                    if (attempt == maxAttempts)
                    {
                        throw new ReviewTranslationProviderException(
                            $"Azure Translator returned status code {(int)response.StatusCode}.");
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new ReviewTranslationProviderException(
                        $"Azure Translator returned status code {(int)response.StatusCode}.");
                }

                var responsePayload = await response.Content.ReadFromJsonAsync<AzureTranslateResponseItem[]>(
                    SerializerOptions,
                    cancellationToken);

                if (responsePayload is null || responsePayload.Length != texts.Count)
                {
                    throw new ReviewTranslationProviderException("Azure Translator returned an invalid batch response.");
                }

                var results = new List<AzureTranslatorTranslationItem>(responsePayload.Length);
                foreach (var item in responsePayload)
                {
                    var translation = item.Translations is { Count: > 0 } translations
                        ? translations[0]
                        : null;
                    results.Add(new AzureTranslatorTranslationItem(
                        translation?.Text ?? string.Empty,
                        item.DetectedLanguage?.Language));
                }

                return results;
            }
        }

        throw new ReviewTranslationProviderException("Azure Translator request failed after retries.");
    }

    private static string BuildTranslateUri(string endpoint, string targetLanguage, string? sourceLanguage)
    {
        var baseEndpoint = endpoint.TrimEnd('/');
        var uri = $"{baseEndpoint}/translate?api-version=3.0&to={Uri.EscapeDataString(targetLanguage)}";
        if (!string.IsNullOrWhiteSpace(sourceLanguage))
        {
            uri += $"&from={Uri.EscapeDataString(sourceLanguage)}";
        }

        return uri;
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

public sealed record AzureTranslatorTranslationItem(string Text, string? DetectedSourceLanguage);
