using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MovieApp.Infrastructure.Email;

namespace MovieApp.UnitTests.Email;

internal static class MovieCaveVerificationEmailHtmlStructure
{
    private static readonly Regex DuplicateStyleAttributePattern = new(
        @"\sstyle\s*=\s*""[^""]*""[^>]*\sstyle\s*=\s*""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DuplicateBgColorAttributePattern = new(
        @"\sbgcolor\s*=\s*""[^""]*""[^>]*\sbgcolor\s*=\s*""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void AssertStructurallyValid(string html, string verifyUrl, string? logoImageUrl = null)
    {
        Assert.False(DuplicateStyleAttributePattern.IsMatch(html), "Generated HTML contains duplicate style attributes on the same tag.");
        Assert.False(DuplicateBgColorAttributePattern.IsMatch(html), "Generated HTML contains duplicate bgcolor attributes on the same tag.");

        var document = new HtmlDocument
        {
            OptionCheckSyntax = true,
            OptionFixNestedTags = false
        };

        document.LoadHtml(html);
        Assert.NotNull(document.DocumentNode);

        if (!string.IsNullOrWhiteSpace(logoImageUrl))
        {
            var headerLogo = document.DocumentNode.SelectSingleNode(
                $"//img[contains(concat(' ', normalize-space(@class), ' '), ' {MovieCaveVerificationEmailContent.HeaderLogoMarkerClass} ')]");
            Assert.NotNull(headerLogo);
            Assert.Equal(logoImageUrl, headerLogo.GetAttributeValue("src", string.Empty));
            Assert.Equal("Movie Cave", headerLogo.GetAttributeValue("alt", string.Empty));
            Assert.Equal(
                MovieCaveVerificationEmailContent.HeaderLogoDisplayWidthPx.ToString(CultureInfo.InvariantCulture),
                headerLogo.GetAttributeValue("width", string.Empty));

            var logoStyle = headerLogo.GetAttributeValue("style", string.Empty);
            Assert.Contains(
                $"width:{MovieCaveVerificationEmailContent.HeaderLogoDisplayWidthPx}px",
                logoStyle,
                StringComparison.Ordinal);
        }

        var badgeTable = document.DocumentNode.SelectSingleNode(
            $"//table[contains(concat(' ', normalize-space(@class), ' '), ' {MovieCaveVerificationEmailContent.EnvelopeBadgeMarkerClass} ')]");
        Assert.NotNull(badgeTable);
        Assert.Equal("48", badgeTable.GetAttributeValue("width", string.Empty));
        Assert.DoesNotContain("width=\"100%\"", badgeTable.OuterHtml, StringComparison.Ordinal);

        var ctaTable = document.DocumentNode.SelectSingleNode(
            $"//table[contains(concat(' ', normalize-space(@class), ' '), ' {MovieCaveVerificationEmailContent.CtaButtonMarkerClass} ')]");
        Assert.NotNull(ctaTable);
        Assert.Equal("320", ctaTable.GetAttributeValue("width", string.Empty));

        var ctaCell = ctaTable.SelectSingleNode(".//td");
        Assert.NotNull(ctaCell);
        Assert.Equal(MovieCaveVerificationEmailContent.GoldAccentColor, ctaCell.GetAttributeValue("bgcolor", string.Empty));

        var ctaLink = ctaTable.SelectSingleNode(".//a[contains(@class, 'cta-button-link')]");
        Assert.NotNull(ctaLink);
        Assert.Equal(verifyUrl, ctaLink.GetAttributeValue("href", string.Empty));

        var linkStyle = ctaLink.GetAttributeValue("style", string.Empty);
        Assert.Contains("display:block", linkStyle, StringComparison.Ordinal);
        Assert.Contains("width:100%", linkStyle, StringComparison.Ordinal);
        Assert.Contains("box-sizing:border-box", linkStyle, StringComparison.Ordinal);

        var featureRow = document.DocumentNode.SelectSingleNode(
            $"//table[contains(concat(' ', normalize-space(@class), ' '), ' {MovieCaveVerificationEmailContent.FeatureRowMarkerClass} ')]/tr");
        Assert.NotNull(featureRow);

        var featureCells = featureRow.SelectNodes("./td");
        Assert.NotNull(featureCells);
        Assert.Equal(3, featureCells.Count);

        foreach (var cell in featureCells)
        {
            Assert.Equal("33%", cell.GetAttributeValue("width", string.Empty));
            Assert.Contains("width:33.333%", cell.GetAttributeValue("style", string.Empty), StringComparison.Ordinal);
            Assert.Equal("center", cell.GetAttributeValue("align", string.Empty));
        }

        Assert.Equal(CountTag(html, "<table"), CountTag(html, "</table>"));
        Assert.Equal(CountTag(html, "<tr"), CountTag(html, "</tr>"));
        Assert.Equal(CountTag(html, "<td"), CountTag(html, "</td>"));
    }

    private static int CountTag(string html, string tagPrefix) =>
        Regex.Count(html, tagPrefix, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
