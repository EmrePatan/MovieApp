using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class VerificationEmailHeroUrlResolverTests
{
    [Fact]
    public void ResolveReturnsNullWhenHeroImageUrlIsEmpty()
    {
        Assert.Null(VerificationEmailHeroUrlResolver.Resolve(null, "https://api.example.com"));
        Assert.Null(VerificationEmailHeroUrlResolver.Resolve("   ", "https://api.example.com"));
    }

    [Fact]
    public void ResolveReturnsAbsoluteUrlWhenConfigured()
    {
        const string heroUrl = "https://cdn.example.com/movie-cave/email-hero.jpg";

        Assert.Equal(heroUrl, VerificationEmailHeroUrlResolver.Resolve(heroUrl, "https://api.example.com"));
    }

    [Fact]
    public void ResolveCombinesRelativePathWithPublicBaseUrl()
    {
        Assert.Equal(
            "https://movieapp-fpkg.onrender.com/email-assets/verification-hero.jpg",
            VerificationEmailHeroUrlResolver.Resolve(
                VerificationEmailHeroUrlResolver.DefaultHeroImagePath,
                "https://movieapp-fpkg.onrender.com"));
    }

    [Fact]
    public void ResolveReturnsNullWhenRelativePathHasNoPublicBaseUrl()
    {
        Assert.Null(VerificationEmailHeroUrlResolver.Resolve(
            VerificationEmailHeroUrlResolver.DefaultHeroImagePath,
            publicBaseUrl: null));
    }

    [Fact]
    public void ResolveReturnsNullForUnsupportedSchemes()
    {
        Assert.Null(VerificationEmailHeroUrlResolver.Resolve("ftp://assets.example.com/hero.jpg", "https://api.example.com"));
    }
}
