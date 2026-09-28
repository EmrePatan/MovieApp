using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.CatalogShare;

public static class CatalogShareWebUrls
{
    public static string ResolveAppOpenWebBaseUrl(CatalogShareOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PublicAppOpenWebBaseUrl))
        {
            return options.PublicAppOpenWebBaseUrl.TrimEnd('/');
        }

        if (!Uri.TryCreate(options.PublicWebBaseUrl.Trim(), UriKind.Absolute, out var canonicalUri))
        {
            return string.Empty;
        }

        var openHost = $"open.{canonicalUri.Host}";
        return $"{canonicalUri.Scheme}://{openHost}";
    }

    public static string BuildCanonicalUrl(CatalogShareOptions options, string segment, Guid id)
    {
        var baseUrl = options.PublicWebBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{segment}/{id:D}";
    }

    public static string BuildAppOpenUrl(CatalogShareOptions options, string segment, Guid id)
    {
        var baseUrl = ResolveAppOpenWebBaseUrl(options);
        return $"{baseUrl}/{segment}/{id:D}";
    }

    public static string BuildWatchlistCanonicalUrl(CatalogShareOptions options, string token)
    {
        var baseUrl = options.PublicWebBaseUrl.TrimEnd('/');
        return $"{baseUrl}/watchlist/{token}";
    }

    /// <summary>
    /// Square app icon used for watchlist link previews (og:image). Kept in sync with mobile <c>assets/icon.png</c>.
    /// </summary>
    public static string BuildPublicAppIconImageUrl(CatalogShareOptions options)
    {
        var baseUrl = options.PublicWebBaseUrl.TrimEnd('/');
        return $"{baseUrl}/assets/images/movie-cave-app-icon.png";
    }

    public static string BuildWatchlistAppOpenUrl(CatalogShareOptions options, string token)
    {
        var baseUrl = ResolveAppOpenWebBaseUrl(options);
        return $"{baseUrl}/watchlist/{token}";
    }

    public static bool IsAppOpenRequestHost(string? requestHost, CatalogShareOptions options)
    {
        if (string.IsNullOrWhiteSpace(requestHost))
        {
            return false;
        }

        if (!Uri.TryCreate(ResolveAppOpenWebBaseUrl(options), UriKind.Absolute, out var openUri))
        {
            return false;
        }

        return string.Equals(requestHost, openUri.Host, StringComparison.OrdinalIgnoreCase);
    }
}
