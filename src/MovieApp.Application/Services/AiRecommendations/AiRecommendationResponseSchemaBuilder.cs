namespace MovieApp.Application.Services.AiRecommendations;

public static class AiRecommendationResponseSchemaBuilder
{
    public static object BuildGeminiResponseSchema(int maxSuggestions) =>
        new
        {
            type = "object",
            properties = new
            {
                suggestions = new
                {
                    type = "array",
                    maxItems = maxSuggestions,
                    items = BuildSuggestionItemSchema()
                },
                constraintUpdates = BuildConstraintUpdatesSchema()
            },
            required = new[] { "suggestions" }
        };

    public static object BuildOpenAiJsonSchema(int maxSuggestions) =>
        new
        {
            type = "object",
            properties = new
            {
                suggestions = new
                {
                    type = "array",
                    maxItems = maxSuggestions,
                    items = BuildSuggestionItemSchema()
                },
                constraintUpdates = BuildConstraintUpdatesSchema()
            },
            required = new[] { "suggestions" }
        };

    private static object BuildSuggestionItemSchema() =>
        new
        {
            type = "object",
            properties = new
            {
                title = new { type = "string" },
                year = new { type = "integer" },
                mediaType = new { type = "string", @enum = new[] { "movie", "tv" } },
                tmdbId = new { type = "integer" },
                reason = new { type = "string" }
            },
            required = new[] { "title", "year", "mediaType", "reason" }
        };

    private static object BuildConstraintUpdatesSchema() =>
        new
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
        };
}
