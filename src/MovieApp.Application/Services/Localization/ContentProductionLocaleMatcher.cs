namespace MovieApp.Application.Services.Localization;

internal static class ContentProductionLocaleMatcher
{
    public static bool ProductionCountryMatchesRequestedLocale(
        string? primaryOriginCountryCode,
        string contentLocale)
    {
        if (string.IsNullOrWhiteSpace(primaryOriginCountryCode))
        {
            return false;
        }

        var regionCode = ExtractRegionCode(contentLocale);
        if (regionCode is null)
        {
            return false;
        }

        return string.Equals(
            primaryOriginCountryCode.Trim(),
            regionCode,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractRegionCode(string contentLocale)
    {
        var normalized = ContentLocaleResolver.Normalize(contentLocale);
        var segments = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        return segments[^1].ToUpperInvariant();
    }
}
