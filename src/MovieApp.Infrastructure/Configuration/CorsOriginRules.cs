namespace MovieApp.Infrastructure.Configuration;

internal static class CorsOriginRules
{
    internal static bool IsValidOrigin(string? origin, bool requireHttps)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var trimmed = origin.Trim();

        if (trimmed.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requireHttps &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
