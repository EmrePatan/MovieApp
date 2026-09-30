using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Keywords;

public static class KeywordDiscoverLocalizationSupport
{
    public static string NormalizeLocale(string? contentLocale) =>
        SupportedContentLocales.Normalize(contentLocale);

    public static string NormalizeSearchName(string name) =>
        KeywordCanonicalNormalization.NormalizeKeywordName(name);

    public static string ResolveDisplayName(
        string? requestedLocaleName,
        string? englishLocaleName,
        string? canonicalName,
        string name)
    {
        if (!string.IsNullOrWhiteSpace(requestedLocaleName))
        {
            return requestedLocaleName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(englishLocaleName))
        {
            return englishLocaleName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(canonicalName))
        {
            return canonicalName.Trim();
        }

        return name.Trim();
    }

    public static bool MatchesQuery(
        string query,
        string normalizedQuery,
        string displayName,
        string keywordName,
        string? canonicalName,
        string? requestedLocaleName,
        string? requestedLocaleNormalized,
        string? englishLocaleName,
        string? englishLocaleNormalized)
    {
        if (ContainsIgnoreCase(displayName, query) ||
            ContainsIgnoreCase(keywordName, query) ||
            (canonicalName is not null && ContainsIgnoreCase(canonicalName, query)))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(normalizedQuery))
        {
            if (requestedLocaleNormalized is not null &&
                requestedLocaleNormalized.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                return true;
            }

            if (englishLocaleNormalized is not null &&
                englishLocaleNormalized.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (requestedLocaleName is not null && ContainsIgnoreCase(requestedLocaleName, query))
        {
            return true;
        }

        if (englishLocaleName is not null && ContainsIgnoreCase(englishLocaleName, query))
        {
            return true;
        }

        return false;
    }

    public static int ComputeRankScore(
        string query,
        string normalizedQuery,
        string displayName,
        string keywordName,
        string? canonicalName,
        string? requestedLocaleName,
        string? englishLocaleName)
    {
        if (EqualsIgnoreCase(displayName, query))
        {
            return 0;
        }

        if (StartsWithIgnoreCase(displayName, query))
        {
            return 1;
        }

        if (ContainsIgnoreCase(displayName, query))
        {
            return 2;
        }

        if (requestedLocaleName is not null)
        {
            if (EqualsIgnoreCase(requestedLocaleName, query))
            {
                return 3;
            }

            if (StartsWithIgnoreCase(requestedLocaleName, query))
            {
                return 4;
            }

            if (ContainsIgnoreCase(requestedLocaleName, query))
            {
                return 5;
            }
        }

        if (canonicalName is not null)
        {
            if (EqualsIgnoreCase(canonicalName, query))
            {
                return 6;
            }

            if (StartsWithIgnoreCase(canonicalName, query))
            {
                return 7;
            }

            if (ContainsIgnoreCase(canonicalName, query))
            {
                return 8;
            }
        }

        if (EqualsIgnoreCase(keywordName, query))
        {
            return 9;
        }

        if (StartsWithIgnoreCase(keywordName, query))
        {
            return 10;
        }

        if (englishLocaleName is not null)
        {
            if (EqualsIgnoreCase(englishLocaleName, query))
            {
                return 11;
            }

            if (StartsWithIgnoreCase(englishLocaleName, query))
            {
                return 12;
            }

            if (ContainsIgnoreCase(englishLocaleName, query))
            {
                return 13;
            }
        }

        if (!string.IsNullOrEmpty(normalizedQuery) &&
            KeywordCanonicalNormalization.NormalizeKeywordName(displayName).Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 14;
        }

        return 15;
    }

    private static bool EqualsIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithIgnoreCase(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIgnoreCase(string value, string substring) =>
        value.Contains(substring, StringComparison.OrdinalIgnoreCase);
}
