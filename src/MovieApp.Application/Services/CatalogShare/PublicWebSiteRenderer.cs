using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.CatalogShare;

public sealed class PublicWebSiteRenderer(IOptions<CatalogShareOptions> catalogShareOptions)
{
    private const string SharedStyles =
        "<style>:root{color-scheme:dark;--bg:#0a0a0f;--text:#f4f4f5;--muted:#a1a1aa;--accent:#e8c872;--card:#12121a;--border:rgba(255,255,255,.08)}"
        + "body{margin:0;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--text)}"
        + "a{color:inherit;text-decoration:none}.site{max-width:1100px;margin:0 auto;padding:20px 16px 48px}"
        + ".top{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:28px;flex-wrap:wrap}"
        + ".logo{font-size:.9rem;letter-spacing:.14em;text-transform:uppercase;font-weight:700;color:var(--accent)}"
        + ".nav{display:flex;gap:18px;font-size:.95rem}.nav a{color:var(--muted)}.nav a:hover{color:var(--text)}"
        + ".cta{display:inline-block;padding:10px 18px;border-radius:999px;background:var(--accent);color:#1a1408;font-weight:700}"
        + ".hero{margin-bottom:36px}.hero h1{font-size:clamp(1.6rem,4vw,2.4rem);margin:0 0 10px;line-height:1.15}"
        + ".hero p{color:var(--muted);max-width:52ch;line-height:1.55;margin:0}"
        + ".section{margin-bottom:36px}.section h2{font-size:1.15rem;margin:0 0 14px;color:var(--text)}"
        + ".rail{display:flex;gap:14px;overflow-x:auto;padding-bottom:6px;scroll-snap-type:x mandatory}"
        + ".card{flex:0 0 132px;scroll-snap-align:start;background:var(--card);border:1px solid var(--border);border-radius:12px;overflow:hidden}"
        + ".grid>.card{flex:unset}"
        + ".card img{width:100%;aspect-ratio:2/3;object-fit:cover;display:block;background:#1a1a24}"
        + ".card-body{padding:10px}.card-title{font-size:.85rem;font-weight:600;line-height:1.25;margin:0 0 4px}"
        + ".card-meta{font-size:.75rem;color:var(--muted)}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(132px,1fr));gap:14px}"
        + ".value{padding:28px 20px;border-radius:16px;background:linear-gradient(135deg,#14141f,#0f0f16);border:1px solid var(--border)}"
        + ".value h2{margin:0 0 14px;font-size:1.25rem}.value ul{margin:0;padding-left:18px;color:var(--muted);line-height:1.7}"
        + ".detail-card{display:flex;gap:20px;flex-wrap:wrap;align-items:flex-start}.detail-poster{width:180px;border-radius:12px;border:1px solid var(--border)}"
        + ".detail-meta{flex:1;min-width:240px}.detail-meta h1{margin:0 0 8px;font-size:1.7rem}.muted{color:var(--muted)}"
        + ".actions{margin-top:22px}.button-primary{display:inline-block;padding:12px 18px;border-radius:999px;background:var(--accent);color:#1a1408;font-weight:700}"
        + ".footer{margin-top:40px;font-size:.8rem;color:var(--muted);text-align:center}</style>";

    public string RenderLanding(PublicWebLandingPageModel model, string? acceptLanguageHeader)
    {
        var shareOptions = catalogShareOptions.Value;
        var site = PublicWebSiteCopy.Resolve(acceptLanguageHeader);
        var language = CatalogShareWebCopy.HtmlLang(CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader));
        var canonical = model.CanonicalPageUrl;

        var builder = new StringBuilder();
        AppendHead(builder, site.SiteTitle, site.SiteDescription, canonical, null, language);
        builder.Append("<body><div class=\"site\">");
        AppendHeader(builder, site, model.OpenAppUrl);
        builder.Append("<section class=\"hero\"><h1>").Append(Encode(site.SiteTitle)).Append("</h1><p>")
            .Append(Encode(site.SiteDescription)).Append("</p></section>");
        AppendRailSection(builder, site.TrendingMovies, model.TrendingMovies, site, shareOptions);
        AppendRailSection(builder, site.TrendingTv, model.TrendingTvShows, site, shareOptions);
        AppendValueSection(builder, site);
        AppendFooter(builder);
        builder.Append("</div></body></html>");
        return builder.ToString();
    }

    public string RenderListing(PublicWebCatalogListingPageModel model, string? acceptLanguageHeader)
    {
        var site = PublicWebSiteCopy.Resolve(acceptLanguageHeader);
        var language = CatalogShareWebCopy.HtmlLang(CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader));

        var builder = new StringBuilder();
        var shareOptions = catalogShareOptions.Value;
        var openAppUrl = ResolveListingOpenAppUrl(model);
        AppendHead(builder, model.PageTitle, model.PageDescription, model.CanonicalPageUrl, null, language);
        builder.Append("<body><div class=\"site\">");
        AppendHeader(builder, site, openAppUrl);
        builder.Append("<section class=\"section\"><h1>").Append(Encode(model.PageTitle.Split('—')[0].Trim()))
            .Append("</h1><p class=\"muted\">").Append(Encode(model.PageDescription)).Append("</p></section>");
        builder.Append("<section class=\"section\"><div class=\"grid\">");
        foreach (var card in model.Items)
        {
            AppendGridCard(builder, card, site, shareOptions);
        }

        builder.Append("</div></section>");
        AppendFooter(builder);
        builder.Append("</div></body></html>");
        return builder.ToString();
    }

    public string RenderContentPage(CatalogPublicSharePageModel model, string? acceptLanguageHeader)
    {
        var detail = CatalogShareWebCopy.Resolve(acceptLanguageHeader);
        var site = PublicWebSiteCopy.Resolve(acceptLanguageHeader);
        var language = CatalogShareWebCopy.HtmlLang(CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader));
        var typeLabel = model.ContentKind == CatalogShareContentKind.Movie ? detail.MovieLabel : detail.TvLabel;
        var year = CatalogShareWebCopy.FormatYear(model.Year);
        var titleWithYear = string.IsNullOrEmpty(year) ? model.Title : $"{model.Title} ({year})";
        var genres = model.Genres.Count > 0 ? string.Join(" · ", model.Genres) : string.Empty;
        var posterUrl = ResolvePosterUrl(model.PosterPath);
        var ogType = model.ContentKind == CatalogShareContentKind.Movie ? "video.movie" : "video.tv_show";
        var rating = FormatRating(model.VoteAverage, site.RatingLabel);

        var builder = new StringBuilder();
        var description = Truncate(model.Overview, 200);
        AppendHead(builder, $"{titleWithYear} — Movie Cave", description, model.CanonicalPageUrl, posterUrl, language, ogType);
        builder.Append("<body><div class=\"site\">");
        AppendHeader(builder, site, model.OpenInAppUrl);
        builder.Append("<article class=\"detail-card\"><img class=\"detail-poster\" src=\"")
            .Append(Encode(posterUrl)).Append("\" alt=\"").Append(Encode(model.Title))
            .Append("\" width=\"180\" height=\"270\" loading=\"lazy\" /><div class=\"detail-meta\"><h1>")
            .Append(Encode(model.Title));
        if (!string.IsNullOrEmpty(year))
        {
            builder.Append(" (").Append(year).Append(')');
        }

        builder.Append("</h1><p class=\"muted\">").Append(Encode(typeLabel));
        if (!string.IsNullOrEmpty(rating))
        {
            builder.Append(" · ").Append(Encode(rating));
        }

        builder.Append("</p>");
        if (!string.IsNullOrEmpty(genres))
        {
            builder.Append("<p class=\"muted\">").Append(Encode(genres)).Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(model.Overview))
        {
            builder.Append("<p>").Append(Encode(model.Overview)).Append("</p>");
        }

        builder.Append("<div class=\"actions\"><a class=\"button-primary\" href=\"")
            .Append(Encode(model.OpenInAppUrl)).Append("\">").Append(Encode(detail.OpenInApp)).Append("</a></div>")
            .Append("</div></article>");
        AppendFooter(builder);
        builder.Append("</div></body></html>");
        return builder.ToString();
    }

    public string RenderNotFoundPage(string? acceptLanguageHeader)
    {
        var copy = CatalogShareWebCopy.Resolve(acceptLanguageHeader);
        var site = PublicWebSiteCopy.Resolve(acceptLanguageHeader);
        var language = CatalogShareWebCopy.HtmlLang(CatalogShareWebCopy.ResolveLanguageCode(acceptLanguageHeader));
        var homeUrl = catalogShareOptions.Value.PublicWebBaseUrl.TrimEnd('/');

        var builder = new StringBuilder();
        builder.Append("<!DOCTYPE html><html lang=\"").Append(language).Append("\"><head><meta charset=\"utf-8\" />")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />")
            .Append("<meta name=\"robots\" content=\"noindex\" />")
            .Append("<title>").Append(Encode(copy.ContentNotFoundTitle)).Append(" — Movie Cave</title>")
            .Append(SharedStyles).Append("</head>");
        builder.Append("<body><div class=\"site\">");
        AppendHeader(builder, site, homeUrl);
        builder.Append("<h1>").Append(Encode(copy.ContentNotFoundTitle)).Append("</h1><p>")
            .Append(Encode(copy.ContentNotFoundBody)).Append("</p>");
        AppendFooter(builder);
        builder.Append("</div></body></html>");
        return builder.ToString();
    }

    private static void AppendHead(
        StringBuilder builder,
        string title,
        string description,
        string canonicalUrl,
        string? ogImage,
        string language,
        string ogType = "website")
    {
        builder.Append("<!DOCTYPE html><html lang=\"").Append(language).Append("\"><head><meta charset=\"utf-8\" />")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />")
            .Append("<title>").Append(Encode(title)).Append("</title>")
            .Append("<meta name=\"description\" content=\"").Append(Encode(description)).Append("\" />")
            .Append("<link rel=\"canonical\" href=\"").Append(Encode(canonicalUrl)).Append("\" />")
            .Append("<meta property=\"og:title\" content=\"").Append(Encode(title)).Append("\" />")
            .Append("<meta property=\"og:description\" content=\"").Append(Encode(description)).Append("\" />")
            .Append("<meta property=\"og:url\" content=\"").Append(Encode(canonicalUrl)).Append("\" />")
            .Append("<meta property=\"og:type\" content=\"").Append(ogType).Append("\" />");
        if (!string.IsNullOrEmpty(ogImage))
        {
            builder.Append("<meta property=\"og:image\" content=\"").Append(Encode(ogImage)).Append("\" />")
                .Append("<meta name=\"twitter:card\" content=\"summary_large_image\" />")
                .Append("<meta name=\"twitter:title\" content=\"").Append(Encode(title)).Append("\" />")
                .Append("<meta name=\"twitter:description\" content=\"").Append(Encode(description)).Append("\" />")
                .Append("<meta name=\"twitter:image\" content=\"").Append(Encode(ogImage)).Append("\" />");
        }

        builder.Append(SharedStyles).Append("</head>");
    }

    private string ResolveListingOpenAppUrl(PublicWebCatalogListingPageModel model)
    {
        if (model.Items.Count == 0)
        {
            return model.CanonicalPageUrl;
        }

        var segment = model.ContentKind == "movie" ? "movie" : "tv";
        return CatalogShareWebUrls.BuildAppOpenUrl(catalogShareOptions.Value, segment, model.Items[0].Id);
    }

    private static void AppendHeader(StringBuilder builder, PublicWebSiteCopy.SiteCopy site, string openAppUrl)
    {
        builder.Append("<header class=\"top\"><a class=\"logo\" href=\"/\">Movie Cave</a><nav class=\"nav\"><a href=\"/movies\">")
            .Append(Encode(site.NavMovies)).Append("</a><a href=\"/tv\">").Append(Encode(site.NavTv))
            .Append("</a></nav><a class=\"cta\" href=\"").Append(Encode(openAppUrl)).Append("\">")
            .Append(Encode(site.OpenMovieCave)).Append("</a></header>");
    }

    private static void AppendRailSection(
        StringBuilder builder,
        string title,
        IReadOnlyList<PublicWebCatalogCard> cards,
        PublicWebSiteCopy.SiteCopy site,
        CatalogShareOptions shareOptions)
    {
        builder.Append("<section class=\"section\"><h2>").Append(Encode(title)).Append("</h2><div class=\"rail\">");
        foreach (var card in cards)
        {
            AppendRailCard(builder, card, site, shareOptions);
        }

        builder.Append("</div></section>");
    }

    private static void AppendRailCard(
        StringBuilder builder,
        PublicWebCatalogCard card,
        PublicWebSiteCopy.SiteCopy site,
        CatalogShareOptions shareOptions)
    {
        var poster = ResolvePosterUrlStatic(card.PosterPath, shareOptions);
        builder.Append("<a class=\"card\" href=\"").Append(Encode(card.CanonicalDetailUrl)).Append("\">")
            .Append("<img src=\"").Append(Encode(poster)).Append("\" alt=\"").Append(Encode(card.Title))
            .Append("\" loading=\"lazy\" /><div class=\"card-body\"><p class=\"card-title\">").Append(Encode(card.Title))
            .Append("</p><p class=\"card-meta\">").Append(Encode(FormatCardMeta(card.Year, card.VoteAverage, site)))
            .Append("</p></div></a>");
    }

    private static void AppendGridCard(
        StringBuilder builder,
        PublicWebCatalogCard card,
        PublicWebSiteCopy.SiteCopy site,
        CatalogShareOptions shareOptions)
    {
        AppendRailCard(builder, card, site, shareOptions);
    }

    private static void AppendValueSection(StringBuilder builder, PublicWebSiteCopy.SiteCopy site)
    {
        builder.Append("<section class=\"value\"><h2>").Append(Encode(site.ValueHeadline)).Append("</h2><ul><li>")
            .Append(Encode(site.ValueDiscover)).Append("</li><li>").Append(Encode(site.ValueRatings))
            .Append("</li><li>").Append(Encode(site.ValueLibrary)).Append("</li><li>")
            .Append(Encode(site.ValueEpisodes)).Append("</li></ul></section>");
    }

    private static void AppendFooter(StringBuilder builder)
    {
        builder.Append("<footer class=\"footer\">Movie Cave</footer>");
    }

    private string ResolvePosterUrl(string? posterPath) => ResolvePosterUrlStatic(posterPath, catalogShareOptions.Value);

    private static string ResolvePosterUrlStatic(string? posterPath, CatalogShareOptions? options = null)
    {
        options ??= new CatalogShareOptions { PublicWebBaseUrl = "https://moviecaveapp.com", ImageCdnBaseUrl = "https://image.tmdb.org/t/p/w500" };
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

    private static string FormatCardMeta(int? year, decimal voteAverage, PublicWebSiteCopy.SiteCopy site)
    {
        var parts = new List<string>();
        if (year is not null)
        {
            parts.Add(year.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (voteAverage > 0)
        {
            parts.Add($"{voteAverage.ToString("0.0", CultureInfo.InvariantCulture)} ★");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : site.RatingLabel;
    }

    private static string FormatRating(decimal? voteAverage, string ratingLabel)
    {
        if (voteAverage is null or <= 0)
        {
            return string.Empty;
        }

        return $"{ratingLabel} {voteAverage.Value.ToString("0.0", CultureInfo.InvariantCulture)} ★";
    }

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "…";
    }
}
