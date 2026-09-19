using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.Tmdb;

public sealed class TmdbCanonicalRequestPathTests
{
    [Fact]
    public void WithCanonicalLanguage_AppendsLanguageWhenQueryMissing()
    {
        var result = TmdbCanonicalRequestPath.WithCanonicalLanguage("movie/157336", "en-US");

        Assert.Equal("movie/157336?language=en-US", result);
    }

    [Fact]
    public void WithCanonicalLanguage_AppendsLanguageWhenOtherQueryParamsExist()
    {
        var result = TmdbCanonicalRequestPath.WithCanonicalLanguage(
            "movie/157336?append_to_response=external_ids",
            "en-US");

        Assert.Equal("movie/157336?append_to_response=external_ids&language=en-US", result);
    }

    [Fact]
    public void WithCanonicalLanguage_ReplacesExistingLanguageParameter()
    {
        var result = TmdbCanonicalRequestPath.WithCanonicalLanguage(
            "search/movie?query=interstellar&language=tr-TR&page=1",
            "en-US");

        Assert.Contains("language=en-US", result, StringComparison.Ordinal);
        Assert.DoesNotContain("tr-TR", result, StringComparison.Ordinal);
        Assert.Contains("query=interstellar", result, StringComparison.Ordinal);
        Assert.Contains("page=1", result, StringComparison.Ordinal);
    }
}
