using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class MovieCaveVerificationEmailContentTests
{
    private const string VerifyUrl = "movieapp://verify-email?token=raw-token-value";
    private const string HeroImageUrl = "https://movieapp-fpkg.onrender.com/email-assets/verification-hero-v2.jpg";
    private const string LogoImageUrl = "https://movieapp-fpkg.onrender.com/email-assets/movie-cave-horizontal-logo-v2.png";

    private static string CommittedSnapshotPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Email", "Snapshots", "movie-cave-verification-email.snapshot.html"));

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
        Assert.DoesNotContain(MovieCaveVerificationEmailContent.HeaderLogoMarkerClass, html, StringComparison.Ordinal);
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
    public void BuildHtmlStructureIsValidForGmailSafeBadgeCtaAndFeatureRow()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, HeroImageUrl, LogoImageUrl);

        MovieCaveVerificationEmailHtmlStructure.AssertStructurallyValid(html, VerifyUrl, LogoImageUrl);
        Assert.Contains(MovieCaveVerificationEmailContent.EnvelopeBadgeMarkerClass, html, StringComparison.Ordinal);
        Assert.Contains(MovieCaveVerificationEmailContent.CtaButtonMarkerClass, html, StringComparison.Ordinal);
        Assert.Contains(MovieCaveVerificationEmailContent.FeatureRowMarkerClass, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Georgia", html, StringComparison.Ordinal);
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
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, HeroImageUrl, LogoImageUrl);

        Assert.Contains($"src=\"{HeroImageUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Movie Cave cinematic hero\"", html, StringComparison.Ordinal);
        Assert.Contains($"src=\"{LogoImageUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Movie Cave\"", html, StringComparison.Ordinal);
        Assert.Contains(MovieCaveVerificationEmailContent.HeaderLogoMarkerClass, html, StringComparison.Ordinal);
        Assert.Contains($"width=\"{MovieCaveVerificationEmailContent.HeaderLogoDisplayWidthPx}\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{VerifyUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains("hero-headline", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTurkishPlainTextIncludesLocalizedCopyAndVerifyUrl()
    {
        var text = MovieCaveVerificationEmailContent.BuildPlainText(
            VerifyUrl,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(
            "Movie Cave e-posta adresini doğrula",
            MovieCaveVerificationEmailContent.GetSubject(ContentLocaleResolver.TurkishTurkey));
        Assert.Contains("Güzel şeylere sadece bir adım kaldı.", text, StringComparison.Ordinal);
        Assert.Contains("E-posta adresini doğrula", text, StringComparison.Ordinal);
        Assert.Contains("Movie Cave'e hoş geldin.", text, StringComparison.Ordinal);
        Assert.Contains("E-posta Adresimi Doğrula:", text, StringComparison.Ordinal);
        Assert.Contains(VerifyUrl, text, StringComparison.Ordinal);
        Assert.Contains("Bu Movie Cave hesabını sen oluşturmadıysan", text, StringComparison.Ordinal);
        Assert.Contains("KEŞFET", text, StringComparison.Ordinal);
        Assert.Contains("Film & Diziler", text, StringComparison.Ordinal);
        Assert.Contains("KAYDET", text, StringComparison.Ordinal);
        Assert.Contains("İzleme Listen", text, StringComparison.Ordinal);
        Assert.Contains("KEYFİNİ ÇIKAR", text, StringComparison.Ordinal);
        Assert.Contains("Sıradaki Favorin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTurkishHtmlIncludesLocalizedCopyWithoutChangingStructure()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(
            VerifyUrl,
            HeroImageUrl,
            LogoImageUrl,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Contains("lang=\"tr\"", html, StringComparison.Ordinal);
        Assert.Contains("Güzel şeylere sadece bir adım kaldı.", html, StringComparison.Ordinal);
        Assert.Contains("E-posta adresini doğrula", html, StringComparison.Ordinal);
        Assert.Contains("Movie Cave&apos;e hoş geldin.", html, StringComparison.Ordinal);
        Assert.Contains("E-posta Adresimi Doğrula &rarr;", html, StringComparison.Ordinal);
        Assert.Contains("Bu Movie Cave hesabını sen oluşturmadıysan", html, StringComparison.Ordinal);
        Assert.Contains("KEŞFET", html, StringComparison.Ordinal);
        Assert.Contains("Film &amp; Diziler", html, StringComparison.Ordinal);
        Assert.Contains("KAYDET", html, StringComparison.Ordinal);
        Assert.Contains("İzleme Listen", html, StringComparison.Ordinal);
        Assert.Contains("KEYFİNİ ÇIKAR", html, StringComparison.Ordinal);
        Assert.Contains("Sıradaki Favorin", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{VerifyUrl}\"", html, StringComparison.Ordinal);
        MovieCaveVerificationEmailHtmlStructure.AssertStructurallyValid(html, VerifyUrl, LogoImageUrl);
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("tr-TR")]
    public void BuildHtmlNormalizesTurkishLocalePrefixes(string contentLocale)
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(
            VerifyUrl,
            heroImageUrl: null,
            contentLocale: contentLocale);

        Assert.Contains("lang=\"tr\"", html, StringComparison.Ordinal);
        Assert.Contains("E-posta Adresimi Doğrula &rarr;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify Email Address &rarr;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildEnglishHtmlRemainsUnchangedFromApprovedSnapshot()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(VerifyUrl, HeroImageUrl, LogoImageUrl);

        Assert.Equal("Verify your Movie Cave email address", MovieCaveVerificationEmailContent.Subject);
        Assert.True(File.Exists(CommittedSnapshotPath), $"Missing snapshot artifact: {CommittedSnapshotPath}");
        Assert.Equal(File.ReadAllText(CommittedSnapshotPath), html);
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
