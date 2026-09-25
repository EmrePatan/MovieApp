using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Authentication;

public sealed class SecurityStampCacheTests
{
    [Fact]
    public void TtlIsFiveSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), SecurityStampCache.Ttl);
    }
}
