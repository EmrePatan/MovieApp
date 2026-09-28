using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.CatalogShare;
namespace MovieApp.Api.CatalogShare;

public static class CatalogShareEndpointRouteBuilderExtensions
{
    private const string AppleAppSiteAssociationPath = "/.well-known/apple-app-site-association";
    private const string AssetLinksPath = "/.well-known/assetlinks.json";

    public static IEndpointRouteBuilder MapCatalogShareEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/movie/{id:guid}", HandleMoviePageAsync);
        endpoints.MapGet("/tv/{id:guid}", HandleTvPageAsync);
        endpoints.MapGet(AppleAppSiteAssociationPath, HandleAppleAppSiteAssociation);
        endpoints.MapGet(AssetLinksPath, HandleAssetLinks);

        return endpoints;
    }

    private static async Task<IResult> HandleMoviePageAsync(
        Guid id,
        HttpContext httpContext,
        [FromServices] ICatalogPublicSharePageService pageService,
        [FromServices] CatalogPublicSharePageRenderer renderer,
        [FromServices] IOptions<CatalogShareOptions> catalogShareOptions,
        CancellationToken cancellationToken)
    {
        if (!IsValidCatalogId(id))
        {
            return NotFoundHtml(httpContext, renderer);
        }

        var openRedirect = TryRedirectAppOpenHostToCanonical(httpContext, catalogShareOptions.Value, "movie", id);
        if (openRedirect is not null)
        {
            return openRedirect;
        }

        var locale = httpContext.Request.Headers.AcceptLanguage.ToString();
        var model = await pageService.TryGetMoviePageAsync(id, locale, cancellationToken);
        if (model is null)
        {
            return NotFoundHtml(httpContext, renderer);
        }

        return HtmlResult(renderer.RenderContentPage(model, locale));
    }

    private static async Task<IResult> HandleTvPageAsync(
        Guid id,
        HttpContext httpContext,
        [FromServices] ICatalogPublicSharePageService pageService,
        [FromServices] CatalogPublicSharePageRenderer renderer,
        [FromServices] IOptions<CatalogShareOptions> catalogShareOptions,
        CancellationToken cancellationToken)
    {
        if (!IsValidCatalogId(id))
        {
            return NotFoundHtml(httpContext, renderer);
        }

        var openRedirect = TryRedirectAppOpenHostToCanonical(httpContext, catalogShareOptions.Value, "tv", id);
        if (openRedirect is not null)
        {
            return openRedirect;
        }

        var locale = httpContext.Request.Headers.AcceptLanguage.ToString();
        var model = await pageService.TryGetTvShowPageAsync(id, locale, cancellationToken);
        if (model is null)
        {
            return NotFoundHtml(httpContext, renderer);
        }

        return HtmlResult(renderer.RenderContentPage(model, locale));
    }

    private static IResult HandleAppleAppSiteAssociation([FromServices] IOptions<CatalogShareOptions> options)
    {
        var teamId = options.Value.IosTeamId?.Trim();
        var payload = new Dictionary<string, object>
        {
            ["applinks"] = new Dictionary<string, object>
            {
                ["apps"] = Array.Empty<string>(),
                ["details"] = string.IsNullOrWhiteSpace(teamId)
                    ? Array.Empty<object>()
                    : new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["appID"] = $"{teamId}.com.movieapp.mobile",
                            ["paths"] = new[] { "/movie/*", "/tv/*" },
                        },
                    },
            },
        };

        return Results.Json(payload, new JsonSerializerOptions { WriteIndented = false });
    }

    private static IResult HandleAssetLinks([FromServices] IOptions<CatalogShareOptions> options)
    {
        var fingerprint = options.Value.AndroidAppLinkSha256Fingerprint?.Trim();
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return Results.Json(Array.Empty<object>());
        }

        var payload = new object[]
        {
            new Dictionary<string, object>
            {
                ["relation"] = new[] { "delegate_permission/common.handle_all_urls" },
                ["target"] = new Dictionary<string, object>
                {
                    ["namespace"] = "android_app",
                    ["package_name"] = "com.movieapp.mobile",
                    ["sha256_cert_fingerprints"] = new[] { fingerprint },
                },
            },
        };

        return Results.Json(payload);
    }

    private static bool IsValidCatalogId(Guid id) => id != Guid.Empty;

    private static IResult? TryRedirectAppOpenHostToCanonical(
        HttpContext httpContext,
        CatalogShareOptions options,
        string segment,
        Guid id)
    {
        if (!CatalogShareWebUrls.IsAppOpenRequestHost(httpContext.Request.Host.Host, options))
        {
            return null;
        }

        var canonicalUrl = CatalogShareWebUrls.BuildCanonicalUrl(options, segment, id);
        return Results.Redirect(canonicalUrl, permanent: false);
    }

    private static IResult NotFoundHtml(HttpContext httpContext, CatalogPublicSharePageRenderer renderer)
    {
        var locale = httpContext.Request.Headers.AcceptLanguage.ToString();
        return Results.Content(renderer.RenderNotFoundPage(locale), "text/html; charset=utf-8", statusCode: StatusCodes.Status404NotFound);
    }

    private static IResult HtmlResult(string html) =>
        Results.Content(html, "text/html; charset=utf-8");
}
