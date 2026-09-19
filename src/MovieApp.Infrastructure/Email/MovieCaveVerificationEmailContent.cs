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
        var cardBackground = DarkSurfaceStyle(CardColor);
        var canvasBackground = DarkSurfaceStyle(CanvasColor);
        var footerBackground = DarkSurfaceStyle("#0D0D10");
        var ctaBackground = DarkSurfaceStyle(GoldAccentColor);

        var builder = new StringBuilder(10_240);
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
                body, .email-bg, .email-bg td {
                  margin: 0 !important;
                  padding: 0 !important;
                  width: 100% !important;
                }
                .dark-surface {
                  background-repeat: repeat !important;
                }
                a.cta-button-link,
                a.cta-button-link span,
                a.cta-button-link font {
                  color: #1A1408 !important;
                  text-decoration: none !important;
                }
                @media only screen and (max-width: 620px) {
                  .email-container { width: 100% !important; max-width: 100% !important; }
                  .outer-padding { padding: 0 !important; }
                  .section-padding { padding-left: 20px !important; padding-right: 20px !important; }
                  .hero-headline { font-size: 18px !important; line-height: 26px !important; }
                  .feature-label { font-size: 10px !important; letter-spacing: 0.12em !important; }
                  .feature-text { font-size: 12px !important; line-height: 18px !important; }
                  .feature-column { padding: 0 4px !important; }
                  .cta-button-cell { padding: 16px 28px !important; }
                }
              </style>
            </head>
            <body class="dark-surface" 
            """);
        builder.Append(canvasBackground);
        builder.Append(" style=\"margin:0;padding:0;width:100%;");
        builder.Append(canvasBackground);
        builder.Append("color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";font-family:");
        builder.Append(SansFontStack);
        builder.Append(";\"><table role=\"presentation\" class=\"email-bg\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        builder.Append(canvasBackground);
        builder.Append(" style=\"width:100%;");
        builder.Append(canvasBackground);
        builder.Append("\"><tr><td align=\"center\" ");
        builder.Append(canvasBackground);
        builder.Append(" style=\"padding:0;margin:0;");
        builder.Append(canvasBackground);
        builder.Append("\">");
        builder.Append("""
              <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;">
                One more step to the good stuff. Verify your Movie Cave email address.
              </div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" 
            """);
        builder.Append(canvasBackground);
        builder.Append(" style=\"width:100%;");
        builder.Append(canvasBackground);
        builder.Append("\"><tr><td align=\"center\" class=\"outer-padding\" style=\"padding:0;margin:0;");
        builder.Append(canvasBackground);
        builder.Append("\"><table role=\"presentation\" class=\"email-container dark-surface\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        builder.Append(cardBackground);
        builder.Append(" style=\"width:600px;max-width:600px;");
        builder.Append(cardBackground);
        builder.Append("border:1px solid #3A3228;\"><tr><td align=\"center\" class=\"section-padding dark-surface\" ");
        builder.Append(cardBackground);
        builder.Append(" style=\"padding:24px 32px 8px 32px;");
        builder.Append(cardBackground);
        builder.Append("\"><p style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:12px;letter-spacing:0.32em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">Movie Cave</p></td></tr>");
        builder.Append(heroSectionHtml);
        builder.Append("<tr><td class=\"section-padding dark-surface\" ");
        builder.Append(cardBackground);
        builder.Append(" style=\"padding:24px 32px 8px 32px;");
        builder.Append(cardBackground);
        builder.Append(BuildEnvelopeBadgeHtml());
        builder.Append("<h1 style=\"margin:0 0 12px 0;font-family:Arial,Helvetica,sans-serif;font-size:24px;line-height:32px;font-weight:700;color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";text-align:center;\">Verify your email address</h1>");
        builder.Append("<p style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:24px;color:");
        builder.Append(SecondaryTextColor);
        builder.Append(";text-align:center;\">Welcome to Movie Cave. Confirm your email to unlock your watchlist, discover movies and TV shows, and start finding your next favorite.</p></td></tr>");
        builder.Append(BuildBulletproofCtaRowHtml(encodedVerifyUrl, cardBackground, ctaBackground));
        builder.Append("<tr><td class=\"section-padding dark-surface\" ");
        builder.Append(cardBackground);
        builder.Append(" style=\"padding:12px 32px 28px 32px;");
        builder.Append(cardBackground);
        builder.Append("\"><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:13px;line-height:22px;color:#AFA79B;text-align:center;\">If you didn&apos;t create a Movie Cave account, you can safely ignore this email.</p></td></tr>");
        builder.Append("<tr><td class=\"section-padding dark-surface\" ");
        builder.Append(cardBackground);
        builder.Append(" style=\"padding:0 20px 24px 20px;");
        builder.Append(cardBackground);
        builder.Append("\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        builder.Append(FeatureColumn("Discover", "Movies &amp; TV Shows", "33%"));
        builder.Append(FeatureColumn("Save", "Your Watchlist", "34%"));
        builder.Append(FeatureColumn("Enjoy", "Your Next Favorite", "33%"));
        builder.Append("</tr></table></td></tr><tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(footerBackground);
        builder.Append(" style=\"padding:18px 32px 24px 32px;");
        builder.Append(footerBackground);
        builder.Append("border-top:1px solid #3A3228;\"><p style=\"margin:0 0 4px 0;font-family:Arial,Helvetica,sans-serif;font-size:11px;letter-spacing:0.24em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">Movie Cave</p><p style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px;color:#AFA79B;\">Your cinematic home for movies and TV.</p></td></tr></table></td></tr></table></td></tr></table></body></html>");

        return builder.ToString();
    }

    private static string BuildHeroSectionHtml(string? heroImageUrl)
    {
        var cardBackground = DarkSurfaceStyle(CardColor);

        if (!string.IsNullOrWhiteSpace(heroImageUrl) &&
            Uri.TryCreate(heroImageUrl.Trim(), UriKind.Absolute, out var heroUri) &&
            (heroUri.Scheme == Uri.UriSchemeHttps || heroUri.Scheme == Uri.UriSchemeHttp))
        {
            var encodedHeroUrl = WebUtility.HtmlEncode(heroUri.ToString());
            var builder = new StringBuilder();
            builder.Append("<tr><td class=\"dark-surface\" ");
            builder.Append(cardBackground);
            builder.Append(" style=\"padding:0;");
            builder.Append(cardBackground);
            builder.Append("\"><img src=\"");
            builder.Append(encodedHeroUrl);
            builder.Append("\" width=\"600\" alt=\"Movie Cave cinematic hero\" style=\"display:block;width:100%;max-width:600px;height:auto;border:0;background-color:");
            builder.Append(CardColor);
            builder.Append(";\" /></td></tr><tr><td class=\"section-padding dark-surface\" ");
            builder.Append(cardBackground);
            builder.Append(" style=\"padding:20px 32px 4px 32px;");
            builder.Append(cardBackground);
            builder.Append("\">");
            builder.Append(BuildHeroHeadlineHtml());
            builder.Append("</td></tr>");
            return builder.ToString();
        }

        var fallbackHero = new StringBuilder();
        fallbackHero.Append("<tr><td class=\"section-padding dark-surface\" ");
        fallbackHero.Append(cardBackground);
        fallbackHero.Append(" style=\"padding:0 8px 4px 8px;");
        fallbackHero.Append(cardBackground);
        fallbackHero.Append("\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        fallbackHero.Append(cardBackground);
        fallbackHero.Append(" style=\"");
        fallbackHero.Append(cardBackground);
        fallbackHero.Append("border-top:1px solid ");
        fallbackHero.Append(GoldAccentColor);
        fallbackHero.Append(";border-bottom:1px solid ");
        fallbackHero.Append(GoldAccentColor);
        fallbackHero.Append(";\"><tr><td align=\"center\" ");
        fallbackHero.Append(cardBackground);
        fallbackHero.Append(" style=\"padding:18px 24px 16px 24px;");
        fallbackHero.Append(cardBackground);
        fallbackHero.Append("\">");
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
        builder.Append(";text-align:center;\"><span style=\"color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";font-weight:700;\">");
        builder.Append(HeroHeadlineText);
        builder.Append("</span></p>");
        return builder.ToString();
    }

    private static string BuildEnvelopeBadgeHtml()
    {
        var builder = new StringBuilder();
        builder.Append("<table role=\"presentation\" width=\"48\" height=\"48\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"margin:0 auto 16px auto;\"><tr><td align=\"center\" valign=\"middle\" width=\"48\" height=\"48\" bgcolor=\"#1A1712\" style=\"width:48px;height:48px;background-color:#1A1712;background-image:linear-gradient(#1A1712,#1A1712);border:1px solid ");
        builder.Append(GoldAccentColor);
        builder.Append(";border-radius:24px;-webkit-border-radius:24px;font-size:20px;line-height:48px;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-weight:700;\"><span style=\"color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-size:20px;line-height:48px;\">&#9993;</span></td></tr></table>");
        return builder.ToString();
    }

    private static string BuildBulletproofCtaRowHtml(
        string encodedVerifyUrl,
        string cardBackground,
        string ctaBackground)
    {
        var builder = new StringBuilder();
        builder.Append("<tr><td align=\"center\" class=\"section-padding dark-surface\" ");
        builder.Append(cardBackground);
        builder.Append(" style=\"padding:20px 32px 12px 32px;");
        builder.Append(cardBackground);
        builder.Append("\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\"><tr><td align=\"center\" ");
        builder.Append(ctaBackground);
        builder.Append(" style=\"border-radius:28px;-webkit-border-radius:28px;\"><!--[if mso]><v:roundrect xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:w=\"urn:schemas-microsoft-com:office:office\" href=\"");
        builder.Append(encodedVerifyUrl);
        builder.Append("\" style=\"height:52px;v-text-anchor:middle;width:300px;\" arcsize=\"50%\" strokecolor=\"");
        builder.Append(GoldAccentColor);
        builder.Append("\" fillcolor=\"");
        builder.Append(GoldAccentColor);
        builder.Append("\"><w:anchorlock/><center style=\"color:");
        builder.Append(CtaTextColor);
        builder.Append(";font-family:Arial,sans-serif;font-size:16px;font-weight:bold;\">Verify Email Address &rarr;</center></v:roundrect><![endif]--><!--[if !mso]><!--><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td align=\"center\" class=\"cta-button-cell\" ");
        builder.Append(ctaBackground);
        builder.Append(" style=\"border-radius:28px;-webkit-border-radius:28px;padding:16px 36px;mso-padding-alt:16px 36px;\"><a class=\"cta-button-link\" href=\"");
        builder.Append(encodedVerifyUrl);
        builder.Append("\" target=\"_blank\" style=\"font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:16px;line-height:20px;font-weight:700;color:");
        builder.Append(CtaTextColor);
        builder.Append(";text-decoration:none;display:block;\"><font color=\"");
        builder.Append(CtaTextColor);
        builder.Append("\"><span style=\"color:");
        builder.Append(CtaTextColor);
        builder.Append(";text-decoration:none;display:inline-block;\">Verify Email Address &rarr;</span></font></a></td></tr></table><!--<![endif]--></td></tr></table></td></tr>");
        return builder.ToString();
    }

    private static string FeatureColumn(string label, string text, string widthPercent)
    {
        var builder = new StringBuilder();
        builder.Append("<td class=\"feature-column\" width=\"");
        builder.Append(widthPercent);
        builder.Append("\" valign=\"top\" style=\"width:");
        builder.Append(widthPercent);
        builder.Append(";padding:0 6px;text-align:center;\"><p class=\"feature-label\" style=\"margin:0 0 4px 0;font-family:Arial,Helvetica,sans-serif;font-size:11px;letter-spacing:0.16em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">");
        builder.Append(label);
        builder.Append("</p><p class=\"feature-text\" style=\"margin:0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:20px;color:");
        builder.Append(SecondaryTextColor);
        builder.Append(";\">");
        builder.Append(text);
        builder.Append("</p></td>");
        return builder.ToString();
    }

    private static string DarkSurfaceStyle(string color) =>
        "bgcolor=\"" + color + "\" style=\"background-color:" + color + ";background-image:linear-gradient(" + color + "," + color + ");\"";
}
