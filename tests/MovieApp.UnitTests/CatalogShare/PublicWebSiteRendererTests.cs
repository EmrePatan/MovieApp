using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.CatalogShare;

namespace MovieApp.UnitTests.CatalogShare;

public sealed class PublicWebSiteRendererTests
{
    private readonly PublicWebSiteRenderer _renderer = new(Options.Create(new CatalogShareOptions
    {
        PublicWebBaseUrl = "https://moviecaveapp.com",
        ImageCdnBaseUrl = "https://image.tmdb.org/t/p/w500",
    }));

    [Fact]
    public void RenderLanding_IncludesCanonicalTrendingAndCta()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var model = new PublicWebLandingPageModel(
            "https://moviecaveapp.com/",
            "https://open.moviecaveapp.com/movie/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            [
                new PublicWebCatalogCard(
                    movieId,
                    "Test Movie",
                    2024,
                    7.5m,
                    "/poster.jpg",
                    "https://moviecaveapp.com/movie/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ],
            []);

        var html = _renderer.RenderLanding(model, "tr");

        Assert.Contains("rel=\"canonical\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://moviecaveapp.com/", html, StringComparison.Ordinal);
        Assert.Contains("Trend Filmler", html, StringComparison.Ordinal);
        Assert.Contains("/movie/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", html, StringComparison.Ordinal);
        Assert.Contains("Movie Cave", html, StringComparison.Ordinal);
        Assert.Contains("open.moviecaveapp.com", html, StringComparison.OrdinalIgnoreCase);
    }
}
