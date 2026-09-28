using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.CatalogShare;

public sealed class CatalogPublicSharePageRenderer(PublicWebSiteRenderer siteRenderer)
{
    public string RenderContentPage(CatalogPublicSharePageModel model, string? acceptLanguageHeader) =>
        siteRenderer.RenderContentPage(model, acceptLanguageHeader);

    public string RenderNotFoundPage(string? acceptLanguageHeader) =>
        siteRenderer.RenderNotFoundPage(acceptLanguageHeader);
}
