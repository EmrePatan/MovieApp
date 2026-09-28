using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.CatalogShare;

namespace MovieApp.UnitTests.CatalogShare;

public sealed class CatalogPublicSharePageRendererTests
{
    private readonly CatalogPublicSharePageRenderer _renderer = new(
        new PublicWebSiteRenderer(Options.Create(new CatalogShareOptions
        {
            PublicWebBaseUrl = "https://moviecaveapp.com",
            ImageCdnBaseUrl = "https://image.tmdb.org/t/p/w500",
            CustomUrlScheme = "movieapp",
        })));

    [Fact]
    public void RenderContentPage_IncludesOpenGraphMetadata()
    {
        var model = new CatalogPublicSharePageModel(
            CatalogShareContentKind.Movie,
            Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            "Inception",
            "A mind-bending thriller.",
            2010,
            ["Sci-Fi", "Thriller"],
            "/inception.jpg",
            8.8m,
            "https://moviecaveapp.com/movie/3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "https://open.moviecaveapp.com/movie/3fa85f64-5717-4562-b3fc-2c963f66afa6");

        var html = _renderer.RenderContentPage(model, "en");

        Assert.Contains("og:title", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("og:image", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Inception", html, StringComparison.Ordinal);
        Assert.Contains("https://image.tmdb.org/t/p/w500/inception.jpg", html, StringComparison.Ordinal);
        Assert.Contains("og:url\" content=\"https://moviecaveapp.com/movie/", html, StringComparison.Ordinal);
        Assert.Contains(
            "class=\"button-primary\" href=\"https://open.moviecaveapp.com/movie/",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RenderNotFoundPage_ReturnsBrandedHtml()
    {
        var html = _renderer.RenderNotFoundPage("tr");

        Assert.Contains("Movie Cave", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"tr\"", html, StringComparison.Ordinal);
        Assert.Contains("bulunamad", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
    }
}
