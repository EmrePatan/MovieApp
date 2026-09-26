using System.Globalization;
using System.Text;

namespace MovieApp.Application.Services.AiRecommendations;

internal static class TitleYearMatcher
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

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

    internal static bool MatchesSearchFallbackAny(
        string suggestionTitle,
        int suggestionYear,
        DateOnly? releaseDate,
        IEnumerable<string?> candidateTitles)
    {
        foreach (var candidateTitle in candidateTitles)
        {
            if (string.IsNullOrWhiteSpace(candidateTitle))
            {
                continue;
            }

            if (MatchesSearchFallback(candidateTitle, null, suggestionTitle, suggestionYear, releaseDate))
            {
                return true;
            }
        }

        return false;
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
            return true;
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

        foreach (var character in title.Trim().ToLower(TurkishCulture))
        {
            if (!char.IsLetterOrDigit(character))
            {
                continue;
            }

            builder.Append(FoldTurkishLetter(character));
        }

        return builder.ToString();
    }

    private static char FoldTurkishLetter(char character) =>
        character switch
        {
            'ç' => 'c',
            'ğ' => 'g',
            'ı' => 'i',
            'ö' => 'o',
            'ş' => 's',
            'ü' => 'u',
            _ => character
        };
}
