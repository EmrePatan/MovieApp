using System.Globalization;

namespace MovieApp.Application.Common;

/// <summary>
/// Turkish-aware case folding for search when the query contains Turkish-specific letters.
/// Not applied globally so English titles (e.g. INTERSTELLAR) are not passed through tr-TR rules.
/// </summary>
public static class SearchTurkishCaseFolder
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static bool MayContainTurkish(string value)
    {
        foreach (var character in value)
        {
            if (IsTurkishSpecificLetter(character))
            {
                return true;
            }
        }

        return false;
    }

    public static string? TryCreateAlternate(string normalizedQuery)
    {
        if (!MayContainTurkish(normalizedQuery))
        {
            return null;
        }

        return TurkishCulture.TextInfo.ToLower(normalizedQuery);
    }

    private static bool IsTurkishSpecificLetter(char character) =>
        character is 'ı'
            or 'İ'
            or 'ğ'
            or 'Ğ'
            or 'ş'
            or 'Ş'
            or 'ç'
            or 'Ç'
            or 'ö'
            or 'Ö'
            or 'ü'
            or 'Ü';
}
