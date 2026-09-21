using HtmlAgilityPack;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

public sealed class MovieCaveEmailCtaHtmlTests
{
    private const string ProductionVerifyUrl =
        "https://moviecaveapp.com/auth/verify-email?token=raw-token-value";

    private const string ProductionResetUrl =
        "https://moviecaveapp.com/auth/reset-password?token=raw-token-value";

    [Theory]
    [InlineData(ProductionVerifyUrl, ContentLocaleResolver.EnglishUnitedStates)]
    [InlineData(ProductionVerifyUrl, ContentLocaleResolver.TurkishTurkey)]
    public void VerificationHtml_ContainsClickableAbsoluteCta(string actionUrl, string contentLocale)
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml(actionUrl, heroImageUrl: null, contentLocale: contentLocale);

        AssertCtaIsValidAbsoluteLink(html, actionUrl);
        Assert.DoesNotContain("Düğme çalışmıyorsa", html, StringComparison.Ordinal);
        Assert.DoesNotContain("If the button doesn", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ProductionResetUrl, ContentLocaleResolver.EnglishUnitedStates)]
    [InlineData(ProductionResetUrl, ContentLocaleResolver.TurkishTurkey)]
    public void PasswordResetHtml_ContainsClickableAbsoluteCta(string actionUrl, string contentLocale)
    {
        var html = MovieCavePasswordResetEmailContent.BuildHtml(actionUrl, heroImageUrl: null, contentLocale: contentLocale);

        AssertCtaIsValidAbsoluteLink(html, actionUrl);
        Assert.DoesNotContain("Düğme çalışmıyorsa", html, StringComparison.Ordinal);
        Assert.DoesNotContain("If the button doesn", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationHtml_DoesNotSilentlyEmitDeadRelativeHref()
    {
        var html = MovieCaveVerificationEmailContent.BuildHtml("?token=dead-link", heroImageUrl: null);

        var document = LoadHtml(html);
        var ctaLink = document.DocumentNode.SelectSingleNode("//a[contains(@class, 'cta-button-link')]");
        Assert.NotNull(ctaLink);

        var href = ctaLink!.GetAttributeValue("href", string.Empty);
        Assert.StartsWith("?token=", href, StringComparison.Ordinal);
        Assert.False(Uri.TryCreate(href, UriKind.Absolute, out _), "Relative href values are not email-safe CTAs.");
    }

    private static void AssertCtaIsValidAbsoluteLink(string html, string expectedUrl)
    {
        var document = LoadHtml(html);
        var ctaLinks = document.DocumentNode.SelectNodes("//a[contains(@class, 'cta-button-link')]");
        Assert.NotNull(ctaLinks);
        Assert.Single(ctaLinks);

        var href = ctaLinks![0].GetAttributeValue("href", string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(href));
        Assert.True(Uri.TryCreate(href, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
        Assert.Equal(expectedUrl, href);
    }

    private static HtmlDocument LoadHtml(string html)
    {
        var document = new HtmlDocument
        {
            OptionCheckSyntax = true,
            OptionFixNestedTags = false
        };
        document.LoadHtml(html);
        return document;
    }
}
