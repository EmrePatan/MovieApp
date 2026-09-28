using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.CatalogShare;

public sealed class CatalogPublicSharePageRenderer(IOptions<CatalogShareOptions> catalogShareOptions)
{
    public string RenderContentPage(CatalogPublicSharePageModel model, string? acceptLanguageHeader)
    {
        var copy = CatalogShareWebCopy.Resolve(acceptLanguageHeader);
        var options = catalogShareOptions.Value;
        var typeLabel = model.ContentKind == CatalogShareContentKind.Movie ? copy.MovieLabel : copy.TvLabel;
        var year = CatalogShareWebCopy.FormatYear(model.Year);
        var titleWithYear = string.IsNullOrEmpty(year) ? model.Title : $"{model.Title} ({year})";
        var genres = model.Genres.Count > 0 ? string.Join(" · ", model.Genres) : string.Empty;
        var posterUrl = ResolvePosterUrl(options, model.PosterPath);
        var language = CatalogShareWebCopy.HtmlLang(CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader));
        var ogType = model.ContentKind == CatalogShareContentKind.Movie ? "video.movie" : "video.tv_show";

        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"").Append(language).Append("\"><head>");
        builder.Append("<meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.Append("<title>").Append(Encode(titleWithYear)).Append(" — Movie Cave</title>");
        builder.Append("<meta name=\"description\" content=\"").Append(Encode(Truncate(model.Overview, 200))).Append("\" />");
        builder.Append("<meta property=\"og:title\" content=\"").Append(Encode(titleWithYear)).Append("\" />");
        builder.Append("<meta property=\"og:description\" content=\"").Append(Encode(Truncate(model.Overview, 200))).Append("\" />");
        builder.Append("<meta property=\"og:image\" content=\"").Append(Encode(posterUrl)).Append("\" />");
        builder.Append("<meta property=\"og:url\" content=\"").Append(Encode(model.CanonicalPageUrl)).Append("\" />");
        builder.Append("<meta property=\"og:type\" content=\"").Append(ogType).Append("\" />");
        builder.Append("<meta name=\"twitter:card\" content=\"summary_large_image\" />");
        builder.Append("<meta name=\"twitter:title\" content=\"").Append(Encode(titleWithYear)).Append("\" />");
        builder.Append("<meta name=\"twitter:description\" content=\"").Append(Encode(Truncate(model.Overview, 200))).Append("\" />");
        builder.Append("<meta name=\"twitter:image\" content=\"").Append(Encode(posterUrl)).Append("\" />");
        builder.Append("<link rel=\"canonical\" href=\"").Append(Encode(model.CanonicalPageUrl)).Append("\" />");
        builder.Append(CatalogSharePageStyles);
        builder.Append("</head><body><main class=\"wrap\"><div class=\"brand\">Movie Cave</div><div class=\"card\">");
        builder.Append("<img class=\"poster\" src=\"").Append(Encode(posterUrl)).Append("\" alt=\"").Append(Encode(model.Title));
        builder.Append("\" width=\"160\" height=\"240\" loading=\"lazy\" /><div class=\"meta\"><h1>").Append(Encode(model.Title));
        if (!string.IsNullOrEmpty(year))
        {
            builder.Append(" (").Append(year).Append(')');
        }

        builder.Append("</h1><p class=\"type\">").Append(Encode(typeLabel)).Append("</p>");
        if (!string.IsNullOrEmpty(genres))
        {
            builder.Append("<p class=\"genres\">").Append(Encode(genres)).Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(model.Overview))
        {
            builder.Append("<p class=\"overview\">").Append(Encode(model.Overview)).Append("</p>");
        }

        builder.Append("<div class=\"actions\"><a class=\"button button-primary\" href=\"")
            .Append(Encode(model.OpenInAppUrl))
            .Append("\">")
            .Append(Encode(copy.OpenInApp))
            .Append("</a></div></div></div></main></body></html>");

        return builder.ToString();
    }

    public string RenderNotFoundPage(string? acceptLanguageHeader)
    {
        var copy = CatalogShareWebCopy.Resolve(acceptLanguageHeader);
        var language = CatalogShareWebCopy.HtmlLang(CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader));
        var homeUrl = catalogShareOptions.Value.PublicWebBaseUrl.TrimEnd('/');

        return "<!DOCTYPE html><html lang=\"" + language + "\"><head><meta charset=\"utf-8\" />"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />"
            + "<title>" + Encode(copy.ContentNotFoundTitle) + " — Movie Cave</title>"
            + "<meta name=\"robots\" content=\"noindex\" />"
            + CatalogSharePageStyles
            + "</head><body><main class=\"wrap\"><div class=\"brand\">Movie Cave</div><h1>"
            + Encode(copy.ContentNotFoundTitle) + "</h1><p>" + Encode(copy.ContentNotFoundBody)
            + "</p><p><a href=\"" + Encode(homeUrl) + "/\">moviecaveapp.com</a></p></main></body></html>";
    }

    private const string CatalogSharePageStyles =
        "<style>:root{color-scheme:dark}body{margin:0;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#0a0a0f;color:#f4f4f5}"
        + ".wrap{max-width:720px;margin:0 auto;padding:24px 20px 40px}.brand{font-size:.85rem;letter-spacing:.12em;text-transform:uppercase;color:#a1a1aa;margin-bottom:20px}"
        + ".card{display:flex;gap:20px;flex-wrap:wrap;align-items:flex-start}.poster{width:160px;border-radius:12px;border:1px solid rgba(255,255,255,.08);box-shadow:0 12px 40px rgba(0,0,0,.45)}"
        + ".meta{flex:1;min-width:220px}h1{margin:0 0 8px;font-size:1.6rem;line-height:1.2}.type{color:#d4d4d8;margin:0 0 8px}"
        + ".genres{color:#a1a1aa;font-size:.95rem;margin:0 0 12px}.overview{color:#e4e4e7;line-height:1.55}.actions{margin-top:24px;display:flex;flex-wrap:wrap;gap:12px}"
        + ".button{display:inline-block;padding:12px 18px;border-radius:999px;text-decoration:none;font-weight:600}"
        + ".button-primary{background:#f4f4f5;color:#0a0a0f}</style>";

    private static string ResolvePosterUrl(CatalogShareOptions options, string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
        {
            return $"{options.PublicWebBaseUrl.TrimEnd('/')}/assets/images/movie-cave-logo.png";
        }

        if (posterPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            posterPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return posterPath;
        }

        var baseUrl = options.ImageCdnBaseUrl.TrimEnd('/');
        var path = posterPath.StartsWith('/') ? posterPath : $"/{posterPath}";
        return $"{baseUrl}{path}";
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength].TrimEnd() + "…";
    }
}
