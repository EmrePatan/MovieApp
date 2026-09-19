namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbCanonicalRequestPath
{
    public static string WithCanonicalLanguage(string relativePath, string canonicalLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalLanguage);

        var pathWithoutLanguage = StripLanguageQueryParameter(relativePath);
        var separator = pathWithoutLanguage.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{pathWithoutLanguage}{separator}language={Uri.EscapeDataString(canonicalLanguage.Trim())}";
    }

    private static string StripLanguageQueryParameter(string relativePath)
    {
        var queryIndex = relativePath.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return relativePath;
        }

        var path = relativePath[..queryIndex];
        var query = relativePath[(queryIndex + 1)..];
        var segments = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !segment.StartsWith("language=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return segments.Count == 0 ? path : $"{path}?{string.Join('&', segments)}";
    }
}
