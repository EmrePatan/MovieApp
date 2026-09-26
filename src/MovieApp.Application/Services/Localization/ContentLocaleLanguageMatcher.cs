namespace MovieApp.Application.Services.Localization;

internal static class ContentLocaleLanguageMatcher
{
    public static bool MatchesOriginalLanguage(string? originalLanguage, string contentLocale)
    {
        if (string.IsNullOrWhiteSpace(originalLanguage))
        {
            return false;
        }

        var contentLanguage = ExtractLanguageCode(ContentLocaleResolver.Normalize(contentLocale));
        var providerLanguage = ExtractLanguageCode(originalLanguage);
        if (contentLanguage is null || providerLanguage is null)
        {
            return false;
        }

        return string.Equals(contentLanguage, providerLanguage, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractLanguageCode(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var separatorIndex = trimmed.IndexOf('-', StringComparison.Ordinal);
        return separatorIndex > 0
            ? trimmed[..separatorIndex].ToLowerInvariant()
            : trimmed.ToLowerInvariant();
    }
}
