namespace MovieApp.Application.Services.Identity;

public static class EmailAuthActionUrlBuilder
{
    public static string Build(string baseUrl, string rawToken)
    {
        var trimmedBase = baseUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBase))
        {
            throw new InvalidOperationException("Email auth action base URL is not configured.");
        }

        var separator = trimmedBase.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{trimmedBase}{separator}token={Uri.EscapeDataString(rawToken)}";
    }

    public static bool IsProductionSafeAbsoluteUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
