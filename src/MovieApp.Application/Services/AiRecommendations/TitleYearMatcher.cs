using System.Globalization;
using System.Text;

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
        if (!TitleMatchesStrict(candidateTitle, candidateOriginalTitle, suggestionTitle))
        {
            return false;
        }

        return YearMatchesStrict(suggestionYear, releaseDate);
    }

    internal static bool MatchesSearchFallback(
        string? candidateTitle,
        string? candidateOriginalTitle,
        string suggestionTitle,
        int suggestionYear,
        DateOnly? releaseDate)
    {
        if (!TitleMatchesSearch(candidateTitle, candidateOriginalTitle, suggestionTitle))
        {
            return false;
        }

        return YearMatchesSearchFallback(suggestionYear, releaseDate);
    }

    private static bool TitleMatchesStrict(
        string? candidateTitle,
        string? candidateOriginalTitle,
        string suggestionTitle)
    {
        var normalizedSuggestion = NormalizeTitleStrict(suggestionTitle);
        if (string.IsNullOrEmpty(normalizedSuggestion))
        {
            return false;
        }

        if (NormalizeTitleStrict(candidateTitle) == normalizedSuggestion)
        {
            return true;
        }

        return NormalizeTitleStrict(candidateOriginalTitle) == normalizedSuggestion;
    }

    private static bool TitleMatchesSearch(
        string? candidateTitle,
        string? candidateOriginalTitle,
        string suggestionTitle)
    {
        var normalizedSuggestion = NormalizeTitleForSearch(suggestionTitle);
        if (string.IsNullOrEmpty(normalizedSuggestion))
        {
            return false;
        }

        if (NormalizeTitleForSearch(candidateTitle) == normalizedSuggestion)
        {
            return true;
        }

        return NormalizeTitleForSearch(candidateOriginalTitle) == normalizedSuggestion;
    }

    private static bool YearMatchesStrict(int suggestionYear, DateOnly? releaseDate)
    {
        if (suggestionYear <= 0)
        {
            return true;
        }

        return releaseDate?.Year == suggestionYear;
    }

    private static bool YearMatchesSearchFallback(int suggestionYear, DateOnly? releaseDate)
    {
        if (suggestionYear <= 0)
        {
            return true;
        }

        if (releaseDate is null)
        {
            return false;
        }

        return Math.Abs(releaseDate.Value.Year - suggestionYear) <= 1;
    }

    private static string NormalizeTitleStrict(string? title) =>
        string.IsNullOrWhiteSpace(title)
            ? string.Empty
            : title.Trim().ToLower(CultureInfo.InvariantCulture);

    internal static string NormalizeTitleForSearch(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(title.Length);

        foreach (var character in title.Trim().ToLower(CultureInfo.InvariantCulture))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
