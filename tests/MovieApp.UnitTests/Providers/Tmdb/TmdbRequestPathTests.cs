using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbRequestPathTests
{
    [Fact]
    public void WithLanguage_CollapsesRawAcceptLanguageHeader()
    {
        var path = TmdbRequestPath.WithLanguage(
            "/search/movie?query=alev",
            "tr-TR,tr;q=0.9,en-US;q=0.8");

        Assert.Equal("/search/movie?query=alev&language=tr-TR", path);
    }
}
