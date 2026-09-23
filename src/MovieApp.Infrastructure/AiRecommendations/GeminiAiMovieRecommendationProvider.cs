using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class GeminiAiMovieRecommendationProvider(
    HttpClient httpClient,
    IOptions<AiRecommendationOptions> options,
    IAiRecommendationPerfContext perfContext,
    ILogger<GeminiAiMovieRecommendationProvider> logger) : IAiMovieRecommendationProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<AiProviderGenerationResult> GenerateAsync(
        AiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var gemini = options.Value.Gemini;
        if (!gemini.Enabled || string.IsNullOrWhiteSpace(gemini.ApiKey))
        {
            throw new AiRecommendationProviderException("Gemini provider is not configured.");
        }

        var modelId = string.IsNullOrWhiteSpace(gemini.ModelId)
            ? "gemini-3.1-flash-lite"
            : gemini.ModelId.Trim();

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={Uri.EscapeDataString(gemini.ApiKey)}";

        var totalStopwatch = Stopwatch.StartNew();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                BuildRequestBody(request),
                Encoding.UTF8,
                "application/json")
        };

        var httpStopwatch = Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        httpStopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            totalStopwatch.Stop();
            perfContext.RecordGeminiTimings(
                totalStopwatch.ElapsedMilliseconds,
                httpStopwatch.ElapsedMilliseconds,
                0);
            GeminiAiMovieRecommendationProviderLogMessages.LogProviderFailure(
                logger,
                response.StatusCode,
                responseBody);
            throw new AiRecommendationProviderException("Gemini provider request failed.");
        }

        var parseStopwatch = Stopwatch.StartNew();
        try
        {
            var result = ParseResponse(responseBody);
            parseStopwatch.Stop();
            totalStopwatch.Stop();
            perfContext.RecordGeminiTimings(
                totalStopwatch.ElapsedMilliseconds,
                httpStopwatch.ElapsedMilliseconds,
                parseStopwatch.ElapsedMilliseconds);

            return result;
        }
        catch
        {
            parseStopwatch.Stop();
            totalStopwatch.Stop();
            perfContext.RecordGeminiTimings(
                totalStopwatch.ElapsedMilliseconds,
                httpStopwatch.ElapsedMilliseconds,
                parseStopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    internal static string BuildRequestBody(AiProviderRequest request)
    {
        var prompt = GeminiPromptBuilder.Build(request);
        var payload = new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = GeminiPromptBuilder.BuildSystemInstruction(request.ResponseLanguage)
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.7,
                responseSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        suggestions = new
                        {
                            type = "array",
                            maxItems = request.SuggestionCount,
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    title = new { type = "string" },
                                    year = new { type = "integer" },
                                    mediaType = new { type = "string" },
                                    tmdbId = new { type = "integer" },
                                    reason = new { type = "string" }
                                },
                                required = new[] { "title", "year", "mediaType", "reason" }
                            }
                        },
                        constraintUpdates = new
                        {
                            type = "object",
                            properties = new
                            {
                                desiredGenres = new { type = "array", items = new { type = "string" } },
                                excludedGenres = new { type = "array", items = new { type = "string" } },
                                maxRuntimeMinutes = new { type = "integer" },
                                minYear = new { type = "integer" },
                                maxYear = new { type = "integer" },
                                moodKeywords = new { type = "array", items = new { type = "string" } }
                            }
                        }
                    },
                    required = new[] { "suggestions" }
                }
            }
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    internal static AiProviderGenerationResult ParseResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var text = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiRecommendationProviderException("Gemini returned empty content.");
        }

        GeminiStructuredResponse? structured;
        try
        {
            structured = JsonSerializer.Deserialize<GeminiStructuredResponse>(text, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new AiRecommendationProviderException($"Gemini returned malformed JSON: {exception.Message}");
        }

        if (structured?.Suggestions is null || structured.Suggestions.Count == 0)
        {
            throw new AiRecommendationProviderException("Gemini returned no suggestions.");
        }

        var suggestions = structured.Suggestions
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.Title))
            .Select(suggestion => new AiProviderSuggestion(
                suggestion.Title.Trim(),
                suggestion.Year,
                suggestion.MediaType?.Trim() ?? string.Empty,
                suggestion.TmdbId,
                suggestion.Reason?.Trim() ?? string.Empty))
            .ToList();

        if (suggestions.Count == 0)
        {
            throw new AiRecommendationProviderException("Gemini returned unusable suggestions.");
        }

        var updates = structured.ConstraintUpdates is null
            ? null
            : new AiProviderConstraintUpdates(
                structured.ConstraintUpdates.DesiredGenres ?? [],
                structured.ConstraintUpdates.ExcludedGenres ?? [],
                structured.ConstraintUpdates.MaxRuntimeMinutes,
                structured.ConstraintUpdates.MinYear,
                structured.ConstraintUpdates.MaxYear,
                structured.ConstraintUpdates.MoodKeywords ?? []);

        return new AiProviderGenerationResult(suggestions, updates);
    }

    private sealed class GeminiStructuredResponse
    {
        [JsonPropertyName("suggestions")]
        public List<GeminiSuggestionResponse>? Suggestions { get; set; }

        [JsonPropertyName("constraintUpdates")]
        public GeminiConstraintUpdatesResponse? ConstraintUpdates { get; set; }
    }

    private sealed class GeminiSuggestionResponse
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("mediaType")]
        public string? MediaType { get; set; }

        [JsonPropertyName("tmdbId")]
        public int? TmdbId { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    private sealed class GeminiConstraintUpdatesResponse
    {
        [JsonPropertyName("desiredGenres")]
        public List<string>? DesiredGenres { get; set; }

        [JsonPropertyName("excludedGenres")]
        public List<string>? ExcludedGenres { get; set; }

        [JsonPropertyName("maxRuntimeMinutes")]
        public int? MaxRuntimeMinutes { get; set; }

        [JsonPropertyName("minYear")]
        public int? MinYear { get; set; }

        [JsonPropertyName("maxYear")]
        public int? MaxYear { get; set; }

        [JsonPropertyName("moodKeywords")]
        public List<string>? MoodKeywords { get; set; }
    }
}

internal static class GeminiPromptBuilder
{
    internal static string BuildSystemInstruction(string responseLanguage)
    {
        var writeReasonsIn = responseLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            ? "Turkish"
            : responseLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "Spanish"
                : "English";

        return $"""
                You are a movie and TV recommendation assistant for MovieApp.
                Return only JSON matching the provided schema.
                Follow the user's request and set mediaType to "movie" or "tv" for each suggestion.
                Write every reason in {writeReasonsIn}.
                Reasons must be short, user-facing, and based only on supplied taste and request information.
                Do not invent claims about the user.
                Include tmdbId whenever you are confident it matches the suggested title and year.
                """;
    }

    internal static string Build(AiProviderRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("User message:");
        builder.AppendLine(request.UserMessage);
        builder.AppendLine();
        builder.AppendLine("Taste profile:");
        AppendTasteProfile(builder, request.TasteProfile);
        builder.AppendLine();
        builder.AppendLine("Session constraints:");
        AppendSessionConstraints(builder, request.Session);
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Return exactly up to {request.SuggestionCount} recommendations (movies and/or TV series).");
        return builder.ToString();
    }

    private static void AppendTasteProfile(StringBuilder builder, AiTasteProfile profile)
    {
        AppendList(builder, "Top genres", profile.TopGenres.Select(item => item.Genre));
        AppendList(builder, "Avoided genres", profile.AvoidedGenres.Select(item => item.Genre));
        AppendMovieSignals(builder, "Highly rated movies", profile.HighRatings);
        AppendMovieSignals(builder, "Low rated movies", profile.LowRatings);
        AppendMovieSignals(builder, "Favorites", profile.Favorites);
        AppendMovieSignals(builder, "Watchlist hints", profile.WatchlistHints);
        AppendList(builder, "Weak TV genre affinity", profile.WeakTvGenres.Select(item => item.Genre));

        if (profile.IsColdStart)
        {
            builder.AppendLine("Cold start: limited taste data available.");
        }
    }

    private static void AppendSessionConstraints(StringBuilder builder, AiRecommendationSessionState session)
    {
        AppendList(builder, "Desired genres", session.DesiredGenres);
        AppendList(builder, "Excluded genres", session.ExcludedGenres);
        AppendList(builder, "Mood keywords", session.MoodKeywords);

        if (session.MaxRuntimeMinutes.HasValue)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Max runtime minutes: {session.MaxRuntimeMinutes.Value}");
        }

        if (session.MinYear.HasValue)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Min year: {session.MinYear.Value}");
        }

        if (session.MaxYear.HasValue)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Max year: {session.MaxYear.Value}");
        }

        if (session.RecommendedMovieIds.Count > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Already recommended movie count: {session.RecommendedMovieIds.Count}");
        }
    }

    private static void AppendList(StringBuilder builder, string label, IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).Take(10).ToList();
        if (items.Count == 0)
        {
            return;
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"{label}: {string.Join(", ", items)}");
    }

    private static void AppendMovieSignals(StringBuilder builder, string label, IReadOnlyList<AiTasteMovieSignal> signals)
    {
        if (signals.Count == 0)
        {
            return;
        }

        builder.AppendLine(label + ":");
        foreach (var signal in signals)
        {
            var year = signal.Year.HasValue ? $" ({signal.Year})" : string.Empty;
            var rating = signal.Rating.HasValue ? $" [{signal.Rating}]" : string.Empty;
            builder.AppendLine(CultureInfo.InvariantCulture, $"- {signal.Title}{year}{rating}");
        }
    }
}

internal static partial class GeminiAiMovieRecommendationProviderLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini provider failed with status {StatusCode}. Body: {Body}")]
    public static partial void LogProviderFailure(ILogger logger, HttpStatusCode statusCode, string body);
}
