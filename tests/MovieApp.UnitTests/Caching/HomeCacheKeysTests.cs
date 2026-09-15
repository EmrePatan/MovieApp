using MovieApp.Application.Caching;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Caching;

public sealed class HomeCacheKeysTests
{
    [Fact]
    public void CreateUsesExpectedFormat()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var key = HomeCacheKeys.Create(userId, SearchContentType.All, 10);

        Assert.Equal($"home:{userId}:All:10:v2", key);
    }
}
