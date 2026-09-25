using System.Globalization;
using System.Text;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

public static class AiRecommendationPromptBuilder
{
    public static string BuildSystemInstruction(string responseLanguage)
    {
        var writeReasonsIn = responseLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase)
            ? "Turkish"
            : responseLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                ? "Spanish"
                : responseLanguage.StartsWith("de", StringComparison.OrdinalIgnoreCase)
                    ? "German"
                    : responseLanguage.StartsWith("fr", StringComparison.OrdinalIgnoreCase)
                        ? "French"
                        : responseLanguage.StartsWith("it", StringComparison.OrdinalIgnoreCase)
                            ? "Italian"
                            : responseLanguage.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                                ? "Brazilian Portuguese"
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

    public static string BuildUserPrompt(AiProviderRequest request)
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

    public static string BuildJsonSchemaDescription(int suggestionCount) =>
        $$"""
        {
          "suggestions": [
            {
              "title": "string",
              "year": 0,
              "mediaType": "movie|tv",
              "tmdbId": 0,
              "reason": "string"
            }
          ],
          "constraintUpdates": {
            "desiredGenres": ["string"],
            "excludedGenres": ["string"],
            "maxRuntimeMinutes": 0,
            "minYear": 0,
            "maxYear": 0,
            "moodKeywords": ["string"]
          }
        }
        Return at most {{suggestionCount}} suggestions. The suggestions array is required.
        """;

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
