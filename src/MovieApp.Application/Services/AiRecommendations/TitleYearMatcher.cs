using System.Globalization;

namespace MovieApp.Application.Services.AiRecommendations;

internal static class TitleYearMatcher
{
    internal static bool Matches(
        string? candidateTitle,
        string? candidateOriginalTitle,
        string suggestionTitle,
        int suggestionYear,
        DateOnly? releaseDate)
    {
        if (!TitleMatches(candidateTitle, candidateOriginalTitle, suggestionTitle))
        {
            return false;
        }

        if (suggestionYear <= 0)
        {
            return true;
        }

        return releaseDate?.Year == suggestionYear;
    }

    private static bool TitleMatches(string? candidateTitle, string? candidateOriginalTitle, string suggestionTitle)
    {
        var normalizedSuggestion = NormalizeTitle(suggestionTitle);
        if (string.IsNullOrEmpty(normalizedSuggestion))
        {
            return false;
        }

        if (NormalizeTitle(candidateTitle) == normalizedSuggestion)
        {
            return true;
        }

        return NormalizeTitle(candidateOriginalTitle) == normalizedSuggestion;
    }

    private static string NormalizeTitle(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim().ToLower(CultureInfo.InvariantCulture);
}
