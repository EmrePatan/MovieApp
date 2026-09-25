using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Services.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class OpenAiCompatibleStructuredRecommendationClient(
    HttpClient httpClient,
    ILogger logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal async Task<(AiProviderGenerationResult Result, long HttpMs, long ParseMs)> SendAsync(
        string providerName,
        Uri endpoint,
        string apiKey,
        string modelId,
        AiProviderRequest request,
        CancellationToken cancellationToken)
    {
        var systemInstruction = AiRecommendationPromptBuilder.BuildSystemInstruction(request.ResponseLanguage);
        var userPrompt = AiRecommendationPromptBuilder.BuildUserPrompt(request);
        var schemaHint = AiRecommendationPromptBuilder.BuildJsonSchemaDescription(request.SuggestionCount);

        var payload = new
        {
            model = modelId,
            temperature = 0.7,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemInstruction + "\n\nJSON schema:\n" + schemaHint },
                new { role = "user", content = userPrompt }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var httpStopwatch = Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        httpStopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            OpenAiCompatibleRecommendationProviderLogMessages.LogProviderFailure(
                logger,
                providerName,
                response.StatusCode,
                TruncateBody(responseBody));
            throw new AiRecommendationProviderHttpException(response.StatusCode);
        }

        var parseStopwatch = Stopwatch.StartNew();
        var content = ExtractMessageContent(responseBody);
        var result = AiStructuredRecommendationParser.Parse(content);
        parseStopwatch.Stop();

        return (result, httpStopwatch.ElapsedMilliseconds, parseStopwatch.ElapsedMilliseconds);
    }

    private static string ExtractMessageContent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiRecommendationProviderException("Provider returned empty content.");
        }

        return content;
    }

    private static string TruncateBody(string body) =>
        body.Length <= 500 ? body : body[..500];
}

internal sealed class AiRecommendationProviderHttpException(HttpStatusCode statusCode) : Exception
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
