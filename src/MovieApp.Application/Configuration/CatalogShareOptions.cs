namespace MovieApp.Application.Configuration;

public sealed class CatalogShareOptions
{
    public const string SectionName = "CatalogShare";

    /// <summary>
    /// Public HTTPS web origin used in share links and Open Graph metadata (for example https://moviecaveapp.com).
    /// </summary>
    public string PublicWebBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Apple Developer Team ID for Universal Links (for example ABCDE12345).
    /// </summary>
    public string IosTeamId { get; set; } = string.Empty;

    /// <summary>
    /// Production Android signing certificate SHA-256 fingerprint for verified App Links.
    /// </summary>
    public string AndroidAppLinkSha256Fingerprint { get; set; } = string.Empty;

    public string CustomUrlScheme { get; set; } = "movieapp";

    /// <summary>
    /// TMDB image CDN base (no trailing path segment); poster paths are appended.
    /// </summary>
    public string ImageCdnBaseUrl { get; set; } = "https://image.tmdb.org/t/p/w500";
}
