using System.Net;
using System.Text;

namespace MovieApp.Infrastructure.Email;

public static class MovieCaveVerificationEmailContent
{
    public const string Subject = "Verify your Movie Cave email address";

    internal const string CanvasColor = "#0A0A0C";
    internal const string CardColor = "#121216";
    internal const string GoldAccentColor = "#E4B84A";
    internal const string CtaTextColor = "#1A1408";
    internal const string PrimaryTextColor = "#FFFFFF";
    internal const string SecondaryTextColor = "#D8D2C8";
    internal const string SansFontStack = "Arial,Helvetica,sans-serif";
    internal const string EnvelopeBadgeMarkerClass = "movie-cave-envelope-badge";
    internal const string CtaButtonMarkerClass = "movie-cave-cta-button";
    internal const string FeatureRowMarkerClass = "movie-cave-feature-row";

    private const string HeroHeadlineText = "One more step to the good stuff.";

    public static string BuildPlainText(string verifyUrl) =>
        """
        MOVIE CAVE

        One more step to the good stuff.

        Verify your email address

        Welcome to Movie Cave. Confirm your email to unlock your watchlist, discover movies and TV shows, and start finding your next favorite.

        Verify your email address:
        """ + verifyUrl + """

        If you didn't create a Movie Cave account, you can safely ignore this email.

        Discover — Movies & TV Shows
        Save — Your Watchlist
        Enjoy — Your Next Favorite

        Movie Cave
        """;

    public static string BuildHtml(string verifyUrl, string? heroImageUrl)
    {
        var encodedVerifyUrl = WebUtility.HtmlEncode(verifyUrl);
        var encodedSubject = WebUtility.HtmlEncode(Subject);
        var heroSectionHtml = BuildHeroSectionHtml(heroImageUrl);

        var builder = new StringBuilder(12_288);
        builder.Append("""
            <!DOCTYPE html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office" style="color-scheme:light only;supported-color-schemes:light only;">
            <head>
              <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <meta name="color-scheme" content="light only" />
              <meta name="supported-color-schemes" content="light only" />
              <meta name="x-apple-disable-message-reformatting" />
              <title>
            """);
        builder.Append(encodedSubject);
        builder.Append("""
            </title>
              <!--[if mso]>
              <noscript>
                <xml>
                  <o:OfficeDocumentSettings>
                    <o:PixelsPerInch>96</o:PixelsPerInch>
                  </o:OfficeDocumentSettings>
                </xml>
              </noscript>
              <![endif]-->
              <style type="text/css">
                :root {
                  color-scheme: light only;
                  supported-color-schemes: light only;
                }
                body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
                table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
                img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
                body {
                  margin: 0 !important;
                  padding: 0 !important;
                  width: 100% !important;
                }
                a.cta-button-link {
                  color: #1A1408 !important;
                  text-decoration: none !important;
                }
                @media only screen and (max-width: 620px) {
                  .email-container { width: 100% !important; max-width: 100% !important; }
                  .section-padding { padding-left: 20px !important; padding-right: 20px !important; }
                  .hero-headline { font-size: 18px !important; line-height: 26px !important; }
                  .feature-label { font-size: 10px !important; letter-spacing: 0.12em !important; }
                  .feature-text { font-size: 12px !important; line-height: 18px !important; }
                }
              </style>
            </head>
            <body 
            """);
        builder.Append(SurfaceAttributes(
            CanvasColor,
            "margin:0;padding:0;width:100%;color:" + PrimaryTextColor + ";font-family:" + SansFontStack + ";"));
        builder.Append("><div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;\">One more step to the good stuff. Verify your Movie Cave email address.</div>");
        builder.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        builder.Append(SurfaceAttributes(CanvasColor, "width:100%;"));
        builder.Append("><tr><td align=\"center\" ");
        builder.Append(SurfaceAttributes(CanvasColor, "padding:0;"));
        builder.Append("><table role=\"presentation\" class=\"email-container\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        builder.Append(SurfaceAttributes(CardColor, "width:600px;max-width:600px;border:1px solid #3A3228;"));
        builder.Append("><tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:24px 32px 8px 32px;"));
        builder.Append("><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:12px;letter-spacing:0.32em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">Movie Cave</p></td></tr>");
        builder.Append(heroSectionHtml);
        builder.Append("<tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:24px 32px 8px 32px;"));
        builder.Append('>');
        builder.Append(BuildEnvelopeBadgeHtml());
        builder.Append("<h1 style=\"margin:0 0 12px 0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:24px;line-height:32px;font-weight:700;color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";text-align:center;\">Verify your email address</h1><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:15px;line-height:24px;color:");
        builder.Append(SecondaryTextColor);
        builder.Append(";text-align:center;\">Welcome to Movie Cave. Confirm your email to unlock your watchlist, discover movies and TV shows, and start finding your next favorite.</p></td></tr>");
        builder.Append(BuildBulletproofCtaRowHtml(encodedVerifyUrl));
        builder.Append("<tr><td class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:12px 32px 28px 32px;"));
        builder.Append("><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:13px;line-height:22px;color:#AFA79B;text-align:center;\">If you didn&apos;t create a Movie Cave account, you can safely ignore this email.</p></td></tr>");
        builder.Append(BuildFeatureRowHtml());
        builder.Append("<tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes("#0D0D10", "padding:18px 32px 24px 32px;border-top:1px solid #3A3228;"));
        builder.Append("><p style=\"margin:0 0 4px 0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:11px;letter-spacing:0.24em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">Movie Cave</p><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:12px;line-height:20px;color:#AFA79B;\">Your cinematic home for movies and TV.</p></td></tr>");
        builder.Append("</table></td></tr></table></body></html>");

        return builder.ToString();
    }

    private static string BuildHeroSectionHtml(string? heroImageUrl)
    {
        if (!string.IsNullOrWhiteSpace(heroImageUrl) &&
            Uri.TryCreate(heroImageUrl.Trim(), UriKind.Absolute, out var heroUri) &&
            (heroUri.Scheme == Uri.UriSchemeHttps || heroUri.Scheme == Uri.UriSchemeHttp))
        {
            var encodedHeroUrl = WebUtility.HtmlEncode(heroUri.ToString());
            var builder = new StringBuilder();
            builder.Append("<tr><td ");
            builder.Append(SurfaceAttributes(CardColor, "padding:0;"));
            builder.Append("><img src=\"");
            builder.Append(encodedHeroUrl);
            builder.Append("\" width=\"600\" alt=\"Movie Cave cinematic hero\" style=\"display:block;width:100%;max-width:600px;height:auto;border:0;background-color:");
            builder.Append(CardColor);
            builder.Append(";\" /></td></tr><tr><td class=\"section-padding\" ");
            builder.Append(SurfaceAttributes(CardColor, "padding:20px 32px 4px 32px;"));
            builder.Append('>');
            builder.Append(BuildHeroHeadlineHtml());
            builder.Append("</td></tr>");
            return builder.ToString();
        }

        var fallbackHero = new StringBuilder();
        fallbackHero.Append("<tr><td class=\"section-padding\" ");
        fallbackHero.Append(SurfaceAttributes(CardColor, "padding:0 8px 4px 8px;"));
        fallbackHero.Append("><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        fallbackHero.Append(SurfaceAttributes(CardColor, "border-top:1px solid " + GoldAccentColor + ";border-bottom:1px solid " + GoldAccentColor + ";"));
        fallbackHero.Append("><tr><td align=\"center\" ");
        fallbackHero.Append(SurfaceAttributes(CardColor, "padding:18px 24px 16px 24px;"));
        fallbackHero.Append('>');
        fallbackHero.Append(BuildHeroHeadlineHtml());
        fallbackHero.Append("</td></tr></table></td></tr>");
        return fallbackHero.ToString();
    }

    private static string BuildHeroHeadlineHtml()
    {
        var builder = new StringBuilder();
        builder.Append("<p class=\"hero-headline\" style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:20px;line-height:28px;font-weight:700;color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";text-align:center;\">");
        builder.Append(HeroHeadlineText);
        builder.Append("</p>");
        return builder.ToString();
    }

    private static string BuildEnvelopeBadgeHtml()
    {
        var builder = new StringBuilder();
        builder.Append("<table role=\"presentation\" class=\"");
        builder.Append(EnvelopeBadgeMarkerClass);
        builder.Append("\" width=\"48\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"width:48px;height:48px;margin:0 auto 16px auto;\"><tr><td align=\"center\" valign=\"middle\" width=\"48\" height=\"48\" bgcolor=\"#1A1712\" style=\"width:48px;height:48px;min-width:48px;max-width:48px;background-color:#1A1712;border:1px solid ");
        builder.Append(GoldAccentColor);
        builder.Append(";border-radius:24px;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:20px;line-height:48px;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">&#9993;</td></tr></table>");
        return builder.ToString();
    }

    private static string BuildBulletproofCtaRowHtml(string encodedVerifyUrl)
    {
        var builder = new StringBuilder();
        builder.Append("<tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:20px 32px 12px 32px;"));
        builder.Append("><table role=\"presentation\" class=\"");
        builder.Append(CtaButtonMarkerClass);
        builder.Append("\" width=\"320\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"width:320px;max-width:320px;\"><tr><td align=\"center\" bgcolor=\"");
        builder.Append(GoldAccentColor);
        builder.Append("\" style=\"background-color:");
        builder.Append(GoldAccentColor);
        builder.Append(";border-radius:28px;\"><a class=\"cta-button-link\" href=\"");
        builder.Append(encodedVerifyUrl);
        builder.Append("\" target=\"_blank\" style=\"display:block;width:100%;box-sizing:border-box;padding:16px 24px;text-align:center;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:16px;line-height:20px;font-weight:700;color:");
        builder.Append(CtaTextColor);
        builder.Append(" !important;text-decoration:none !important;\">Verify Email Address &rarr;</a></td></tr></table></td></tr>");
        return builder.ToString();
    }

    private static string BuildFeatureRowHtml()
    {
        var builder = new StringBuilder();
        builder.Append("<tr><td class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:0 20px 24px 20px;"));
        builder.Append("><table role=\"presentation\" class=\"");
        builder.Append(FeatureRowMarkerClass);
        builder.Append("\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;\"><tr>");
        builder.Append(FeatureColumn("Discover", "Movies &amp; TV Shows"));
        builder.Append(FeatureColumn("Save", "Your Watchlist"));
        builder.Append(FeatureColumn("Enjoy", "Your Next Favorite"));
        builder.Append("</tr></table></td></tr>");
        return builder.ToString();
    }

    private static string FeatureColumn(string label, string text)
    {
        var builder = new StringBuilder();
        builder.Append("<td width=\"33%\" align=\"center\" valign=\"top\" style=\"width:33.333%;padding:0 6px;text-align:center;vertical-align:top;\">");
        builder.Append("<p class=\"feature-label\" style=\"margin:0 0 4px 0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:11px;letter-spacing:0.16em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">");
        builder.Append(label);
        builder.Append("</p><p class=\"feature-text\" style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:13px;line-height:18px;color:");
        builder.Append(SecondaryTextColor);
        builder.Append(";word-break:break-word;\">");
        builder.Append(text);
        builder.Append("</p></td>");
        return builder.ToString();
    }

    private static string SurfaceAttributes(string color, string cssDeclarations) =>
        "bgcolor=\"" + color + "\" style=\"background-color:" + color + ";background-image:linear-gradient(" + color + "," + color + ");" + cssDeclarations + "\"";
}
