using MovieApp.Application.Caching;

namespace MovieApp.UnitTests.Caching;

public sealed class CollectionCacheKeysTests
{
    [Fact]
    public void CreateUsesExpectedFormat()
    {
        var key = CollectionCacheKeys.Create(10001);

        Assert.Equal("collection:10001:v1", key);
        Assert.StartsWith(CollectionCacheKeys.Prefix, key, StringComparison.Ordinal);
        Assert.EndsWith($":{CollectionCacheKeys.Version}", key, StringComparison.Ordinal);
    }
}
