using System.Text.Json;
using System.Text.Json.Serialization;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

public static class AiStructuredRecommendationParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AiProviderGenerationResult Parse(string structuredJson)
    {
        if (string.IsNullOrWhiteSpace(structuredJson))
        {
            throw new AiRecommendationProviderException("Provider returned empty structured content.");
        }

        StructuredResponse? structured;
        try
        {
            structured = JsonSerializer.Deserialize<StructuredResponse>(structuredJson, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new AiRecommendationProviderException($"Provider returned malformed JSON: {exception.Message}");
        }

        if (structured?.Suggestions is null || structured.Suggestions.Count == 0)
        {
            throw new AiRecommendationProviderException("Provider returned no suggestions.");
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
            throw new AiRecommendationProviderException("Provider returned unusable suggestions.");
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

    private sealed class StructuredResponse
    {
        [JsonPropertyName("suggestions")]
        public List<SuggestionResponse>? Suggestions { get; set; }

        [JsonPropertyName("constraintUpdates")]
        public ConstraintUpdatesResponse? ConstraintUpdates { get; set; }
    }

    private sealed class SuggestionResponse
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

    private sealed class ConstraintUpdatesResponse
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
