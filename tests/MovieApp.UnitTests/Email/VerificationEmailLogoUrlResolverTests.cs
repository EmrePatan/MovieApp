using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class VerificationEmailLogoUrlResolverTests
{
    [Fact]
    public void ResolveReturnsNullWhenPublicBaseUrlIsMissing()
    {
        Assert.Null(VerificationEmailLogoUrlResolver.Resolve(null));
        Assert.Null(VerificationEmailLogoUrlResolver.Resolve("   "));
    }

    [Fact]
    public void ResolveBuildsAbsoluteLogoUrlFromPublicBaseUrl()
    {
        Assert.Equal(
            "https://movieapp-fpkg.onrender.com/email-assets/movie-cave-horizontal-logo-v2.png",
            VerificationEmailLogoUrlResolver.Resolve("https://movieapp-fpkg.onrender.com"));
    }
}
