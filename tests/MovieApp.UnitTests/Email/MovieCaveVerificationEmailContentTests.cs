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
        Assert.Contains(MovieCaveVerificationEmailContent.CardColor, html, StringComparison.Ordinal);
        Assert.Contains(MovieCaveVerificationEmailContent.GoldAccentColor, html, StringComparison.Ordinal);
        Assert.Contains(MovieCaveVerificationEmailContent.PrimaryTextColor, html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlUsesGmailSafeLayoutForBadgeCtaAndFeatureRow()
    {
        const string heroImageUrl = "https://cdn.example.com/movie-cave/email-hero.jpg";
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, heroImageUrl);

        Assert.Contains("width=\"48\"", html, StringComparison.Ordinal);
        Assert.Contains("min-width:48px;max-width:48px;height:48px", html, StringComparison.Ordinal);
        Assert.Contains("width=\"320\"", html, StringComparison.Ordinal);
        Assert.Contains("max-width:320px", html, StringComparison.Ordinal);
        Assert.Contains("class=\"cta-button-link\"", html, StringComparison.Ordinal);
        Assert.Contains("<font color=\"" + MovieCaveVerificationEmailContent.CtaTextColor + "\">", html, StringComparison.Ordinal);
        Assert.Contains("a.cta-button-link", html, StringComparison.Ordinal);
        Assert.Contains("text-decoration: none !important", html, StringComparison.Ordinal);
        Assert.Contains("table-layout:fixed", html, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(html, "width=\"33.33%\""));
        Assert.DoesNotContain("Georgia", html, StringComparison.Ordinal);
        Assert.Contains("hero-headline", html, StringComparison.Ordinal);
        Assert.Contains("border-radius:24px", html, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    [Fact]
    public void BuildHtmlUsesGmailDarkModeHardeningAndCompactFallbackHero()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, heroImageUrl: null);

        Assert.Contains("color-scheme: light only", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supported-color-schemes: light only", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bgcolor=\"" + MovieCaveVerificationEmailContent.CardColor + "\"", html, StringComparison.Ordinal);
        Assert.Contains("background-image:linear-gradient(" + MovieCaveVerificationEmailContent.CardColor, html, StringComparison.Ordinal);
        Assert.DoesNotContain("height:180px", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("feature-column { display: block", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("border-top:1px solid " + MovieCaveVerificationEmailContent.GoldAccentColor, html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlUsesConfiguredHeroImageWhenProvided()
    {
        const string heroImageUrl = "https://cdn.example.com/movie-cave/email-hero.jpg";
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, heroImageUrl);

        Assert.Contains($"src=\"{heroImageUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Movie Cave cinematic hero\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{VerifyUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("hero-headline", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlIgnoresInvalidHeroImageUrl()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, "not-a-valid-url");

        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.Contains("One more step to the good stuff.", html, StringComparison.Ordinal);
        Assert.Contains("border-top:1px solid " + MovieCaveVerificationEmailContent.GoldAccentColor, html, StringComparison.Ordinal);
    }
}
