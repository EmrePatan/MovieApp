using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class MovieCavePasswordResetEmailContentTests
{
    private const string ResetUrl = "https://moviecaveapp.com/auth/reset-password?token=raw-token-value";

    [Fact]
    public void BuildPlainTextIncludesResetUrlAndPasswordResetCopy()
    {
        var text = MovieCavePasswordResetEmailContent.BuildPlainText(ResetUrl);

        Assert.Equal("Reset your Movie Cave password", MovieCavePasswordResetEmailContent.Subject);
        Assert.Contains("MOVIE CAVE", text, StringComparison.Ordinal);
        Assert.Contains("Let's get you back in.", text, StringComparison.Ordinal);
        Assert.Contains("Reset your password", text, StringComparison.Ordinal);
        Assert.Contains(ResetUrl, text, StringComparison.Ordinal);
        Assert.Contains("password won't change", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify your email address", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlUsesLinkCopyInsteadOfButtonWording()
    {
        var html = MovieCavePasswordResetEmailContent.BuildHtml(ResetUrl, heroImageUrl: null);

        Assert.Contains("Use the link below", html, StringComparison.Ordinal);
        Assert.DoesNotContain("button below", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildHtmlIncludesResetCtaAndTurkishLocale()
    {
        var html = MovieCavePasswordResetEmailContent.BuildHtml(
            ResetUrl,
            heroImageUrl: null,
            contentLocale: ContentLocaleResolver.TurkishTurkey);

        Assert.Contains("lang=\"tr\"", html, StringComparison.Ordinal);
        Assert.Contains("Şifremi Sıfırla &rarr;", html, StringComparison.Ordinal);
        Assert.Contains("aşağıdaki bağlantıyı kullan", html, StringComparison.Ordinal);
        Assert.DoesNotContain("düğme", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"href=\"{ResetUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains(MovieCavePasswordResetEmailContent.PasswordResetBadgeMarkerClass, html, StringComparison.Ordinal);
        Assert.DoesNotContain("Verify Email Address", html, StringComparison.Ordinal);
    }
}
