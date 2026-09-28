using MovieApp.Application.Configuration;
using MovieApp.Application.Services.CatalogShare;

namespace MovieApp.Application.Services.WatchlistShare;

public static class WatchlistShareUrlBuilder
{
    public static string BuildCanonicalShareUrl(CatalogShareOptions options, string rawToken)
    {
        var baseUrl = options.PublicWebBaseUrl.TrimEnd('/');
        return $"{baseUrl}/watchlist/{rawToken}";
    }

    public static string BuildAppOpenShareUrl(CatalogShareOptions options, string rawToken)
    {
        var baseUrl = CatalogShareWebUrls.ResolveAppOpenWebBaseUrl(options);
        return $"{baseUrl}/watchlist/{rawToken}";
    }
}
