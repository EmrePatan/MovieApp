using MovieApp.Application.Configuration;
using MovieApp.Application.Services.CatalogShare;

namespace MovieApp.UnitTests.CatalogShare;

public sealed class CatalogShareWebUrlsTests
{
    private static readonly Guid MovieId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    [Fact]
    public void BuildCanonicalUrl_UsesPublicWebBaseUrl()
    {
        var options = new CatalogShareOptions { PublicWebBaseUrl = "https://moviecaveapp.com" };
        var url = CatalogShareWebUrls.BuildCanonicalUrl(options, "movie", MovieId);

        Assert.Equal($"https://moviecaveapp.com/movie/{MovieId:D}", url);
    }

    [Fact]
    public void BuildAppOpenUrl_DerivesOpenSubdomain()
    {
        var options = new CatalogShareOptions { PublicWebBaseUrl = "https://moviecaveapp.com" };
        var url = CatalogShareWebUrls.BuildAppOpenUrl(options, "tv", MovieId);

        Assert.Equal($"https://open.moviecaveapp.com/tv/{MovieId:D}", url);
    }

    [Fact]
    public void BuildAppOpenUrl_UsesExplicitOverride()
    {
        var options = new CatalogShareOptions
        {
            PublicWebBaseUrl = "https://moviecaveapp.com",
            PublicAppOpenWebBaseUrl = "https://open.example.test",
        };

        var url = CatalogShareWebUrls.BuildAppOpenUrl(options, "movie", MovieId);
        Assert.Equal($"https://open.example.test/movie/{MovieId:D}", url);
    }

    [Fact]
    public void IsAppOpenRequestHost_MatchesDerivedOpenHost()
    {
        var options = new CatalogShareOptions { PublicWebBaseUrl = "https://moviecaveapp.com" };

        Assert.True(CatalogShareWebUrls.IsAppOpenRequestHost("open.moviecaveapp.com", options));
        Assert.False(CatalogShareWebUrls.IsAppOpenRequestHost("moviecaveapp.com", options));
    }
}
