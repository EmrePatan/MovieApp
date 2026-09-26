using System.Globalization;

namespace MovieApp.Application.Common;

public static class SearchTitleMatching
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static int ComputeRelevanceTier(string title, SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return 0;
        }

        if (TitleEqualsQuery(title, match.Primary)
            || (match.TurkishAlternate is not null && TitleEqualsQuery(title, match.TurkishAlternate)))
        {
            return 0;
        }

        if (TitleStartsWithQuery(title, match.Primary)
            || (match.TurkishAlternate is not null && TitleStartsWithQuery(title, match.TurkishAlternate)))
        {
            return 1;
        }

        return 2;
    }

    public static bool TitleEqualsQuery(string title, string pattern)
    {
        if (title.Length != pattern.Length)
        {
            return false;
        }

        if (string.Equals(title, pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TurkishCultureAwareEquals(title, pattern);
    }

    public static bool TitleStartsWithQuery(string title, string pattern)
    {
        if (title.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pattern.Length > title.Length)
        {
            return false;
        }

        return TurkishCultureAwareEquals(
            title[..pattern.Length],
            pattern);
    }

    private static bool TurkishCultureAwareEquals(string left, string right)
    {
        if (!SearchTurkishCaseFolder.MayContainTurkish(left)
            && !SearchTurkishCaseFolder.MayContainTurkish(right))
        {
            return false;
        }

        var textInfo = TurkishCulture.TextInfo;
        return string.Equals(
            textInfo.ToLower(left),
            textInfo.ToLower(right),
            StringComparison.Ordinal);
    }
}
