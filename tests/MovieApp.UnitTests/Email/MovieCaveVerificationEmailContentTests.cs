using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class MovieCaveVerificationEmailContentTests
{
    private const string VerifyUrl = "movieapp://verify-email?token=raw-token-value";

    [Fact]
    public void BuildPlainTextIncludesVerifyUrlAndMovieCaveBranding()
    {
        var text = MovieCaveVerificationEmailContent.BuildPlainText(VerifyUrl);

        Assert.Equal("Verify your Movie Cave email address", MovieCaveVerificationEmailContent.Subject);
        Assert.Contains("MOVIE CAVE", text, StringComparison.Ordinal);
        Assert.Contains("One more step to the good stuff.", text, StringComparison.Ordinal);
        Assert.Contains("Verify your email address", text, StringComparison.Ordinal);
        Assert.Contains(VerifyUrl, text, StringComparison.Ordinal);
        Assert.Contains("If you didn't create a Movie Cave account", text, StringComparison.Ordinal);
        Assert.Contains("Discover", text, StringComparison.Ordinal);
        Assert.Contains("Your Watchlist", text, StringComparison.Ordinal);
        Assert.Contains("Your Next Favorite", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlIncludesCtaLinkAndDesignCopyWithoutHeroImage()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, heroImageUrl: null);

        Assert.Contains("Movie Cave", html, StringComparison.Ordinal);
        Assert.Contains("One more step to the good stuff.", html, StringComparison.Ordinal);
        Assert.Contains("Verify your email address", html, StringComparison.Ordinal);
        Assert.Contains("Verify Email Address &rarr;", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{VerifyUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("If you didn&apos;t create a Movie Cave account", html, StringComparison.Ordinal);
        Assert.Contains("Discover", html, StringComparison.Ordinal);
        Assert.Contains("Movies &amp; TV Shows", html, StringComparison.Ordinal);
        Assert.Contains("Save", html, StringComparison.Ordinal);
        Assert.Contains("Your Watchlist", html, StringComparison.Ordinal);
        Assert.Contains("Enjoy", html, StringComparison.Ordinal);
        Assert.Contains("Your Next Favorite", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("background-color:#121216", html, StringComparison.Ordinal);
        Assert.Contains("#c8a24a", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlUsesConfiguredHeroImageWhenProvided()
    {
        const string heroImageUrl = "https://cdn.example.com/movie-cave/email-hero.jpg";
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, heroImageUrl);

        Assert.Contains($"src=\"{heroImageUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Movie Cave cinematic hero\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{VerifyUrl}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlIgnoresInvalidHeroImageUrl()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, "not-a-valid-url");

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("One more step to the good stuff.", html, StringComparison.Ordinal);
    }
}
