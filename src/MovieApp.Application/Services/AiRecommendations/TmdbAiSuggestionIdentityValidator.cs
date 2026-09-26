using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.AiRecommendations;

internal static class TmdbAiSuggestionIdentityValidator
{
    internal static bool MatchesMovie(AiProviderSuggestion suggestion, MovieProviderDetails details) =>
        Matches(
            suggestion,
            "movie",
            details.Title,
            details.OriginalTitle,
            details.ReleaseDate,
            details.ProviderSearchTitles);

    internal static bool MatchesTvShow(AiProviderSuggestion suggestion, TvShowProviderDetails details) =>
        Matches(
            suggestion,
            "tv",
            details.Title,
            details.OriginalTitle,
            details.FirstAirDate,
            details.ProviderSearchTitles);

    private static bool Matches(
        AiProviderSuggestion suggestion,
        string expectedMediaType,
        string? title,
        string? originalTitle,
        DateOnly? releaseDate,
        IReadOnlyList<ProviderSearchTitleEntry>? providerSearchTitles)
    {
        if (!string.Equals(
                NormalizeMediaType(suggestion.MediaType),
                expectedMediaType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TitleYearMatcher.MatchesSearchFallback(
                title,
                originalTitle,
                suggestion.Title,
                suggestion.Year,
                releaseDate))
        {
            return true;
        }

        if (providerSearchTitles is not { Count: > 0 })
        {
            return false;
        }

        return TitleYearMatcher.MatchesSearchFallbackAny(
            suggestion.Title,
            suggestion.Year,
            releaseDate,
            providerSearchTitles.Select(entry => entry.Title));
    }

    private static string NormalizeMediaType(string mediaType) =>
        string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
}
