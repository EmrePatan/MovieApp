using MovieApp.Application.Caching;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Caching;

public sealed class HomeGlobalCacheKeysTests
{
    [Fact]
    public void CreateUsesExpectedFormat()
    {
        var key = HomeGlobalCacheKeys.Create(SearchContentType.All, 10, "abc12345");

        Assert.Equal("home-global:All:10:abc12345:v1", key);
    }

    [Fact]
    public void CreateGenreFingerprintDiffersWhenGenreConfigurationChanges()
    {
        var original = HomeGlobalCacheKeys.CreateGenreFingerprint(["Science Fiction", "Action"]);
        var changed = HomeGlobalCacheKeys.CreateGenreFingerprint(["Science Fiction", "Drama"]);

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void CreateGenreFingerprintIsStableForSameConfiguration()
    {
        var first = HomeGlobalCacheKeys.CreateGenreFingerprint(["Comedy", "Drama"]);
        var second = HomeGlobalCacheKeys.CreateGenreFingerprint(["Comedy", "Drama"]);

        Assert.Equal(first, second);
    }
}
