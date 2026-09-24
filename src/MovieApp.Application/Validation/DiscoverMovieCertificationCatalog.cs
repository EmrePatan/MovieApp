namespace MovieApp.Application.Validation;

/// <summary>
/// Curated certification labels accepted for TMDB discover movie filters (per ISO country).
/// </summary>
public static class DiscoverMovieCertificationCatalog
{
    private static readonly Dictionary<string, HashSet<string>> AllowedByCountry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "G", "PG", "PG-13", "R", "NC-17"
            },
            ["GB"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "U", "PG", "12A", "12", "15", "18"
            },
            ["DE"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "0", "6", "12", "16", "18"
            },
            ["FR"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "TP", "12", "16", "18"
            },
            ["TR"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Genel İzleyici", "7+", "13+", "15+", "18+"
            }
        };

    public static bool IsSupportedCountry(string countryCode) =>
        AllowedByCountry.ContainsKey(NormalizeCountry(countryCode));

    public static bool IsAllowed(string? countryCode, string? certification)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(certification))
        {
            return false;
        }

        var normalizedCountry = NormalizeCountry(countryCode);
        if (!AllowedByCountry.TryGetValue(normalizedCountry, out var allowed))
        {
            return false;
        }

        return allowed.Contains(certification.Trim());
    }

    public static string NormalizeCountry(string countryCode) =>
        countryCode.Trim().ToUpperInvariant();
}
