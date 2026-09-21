using System.Net;
using System.Text;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Infrastructure.Email;

public static class MovieCavePasswordResetEmailContent
{
    public const string Subject = "Reset your Movie Cave password";

    internal const string CanvasColor = "#0A0A0C";
    internal const string CardColor = "#121216";
    internal const string GoldAccentColor = "#E4B84A";
    internal const string CtaTextColor = "#1A1408";
    internal const string PrimaryTextColor = "#FFFFFF";
    internal const string SecondaryTextColor = "#D8D2C8";
    internal const string SansFontStack = "Arial,Helvetica,sans-serif";
    internal const string HeaderLogoMarkerClass = "movie-cave-header-logo";
    internal const string PasswordResetBadgeMarkerClass = "movie-cave-password-reset-badge";
    internal const string CtaButtonMarkerClass = "movie-cave-cta-button";
    internal const string FeatureRowMarkerClass = "movie-cave-feature-row";
    internal const int HeaderLogoDisplayWidthPx = 175;

    public static string GetSubject(string contentLocale) =>
        GetCopy(contentLocale).Subject;

    public static string BuildPlainText(
        string resetUrl,
        string contentLocale = ContentLocaleResolver.EnglishUnitedStates)
    {
        var copy = GetCopy(contentLocale);
        return $"""
            MOVIE CAVE

            {copy.HeroHeadline}

            {copy.Title}

            {copy.BodyPlain}

            {copy.PlainTextCtaLabel}
            {resetUrl}

            {copy.Safety}

            {copy.Feature1Label} — {copy.Feature1PlainText}
            {copy.Feature2Label} — {copy.Feature2PlainText}
            {copy.Feature3Label} — {copy.Feature3PlainText}

            Movie Cave
            """;
    }

    public static string BuildHtml(
        string resetUrl,
        string? heroImageUrl,
        string? logoImageUrl = null,
        string contentLocale = ContentLocaleResolver.EnglishUnitedStates)
    {
        var copy = GetCopy(contentLocale);
        var encodedResetUrl = WebUtility.HtmlEncode(resetUrl);
        var encodedSubject = WebUtility.HtmlEncode(copy.Subject);
        var heroSectionHtml = BuildHeroSectionHtml(heroImageUrl, copy);
        var headerLogoHtml = BuildHeaderLogoHtml(logoImageUrl);

        var builder = new StringBuilder(12_288);
        builder.Append("""
            <!DOCTYPE html>
            <html lang="
            """);
        builder.Append(copy.HtmlLang);
        builder.Append("""
            " xmlns="http://www.w3.org/1999/xhtml" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office" style="color-scheme:light only;supported-color-schemes:light only;">
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
        builder.Append("><div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;\">");
        builder.Append(copy.Preheader);
        builder.Append("</div>");
        builder.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        builder.Append(SurfaceAttributes(CanvasColor, "width:100%;"));
        builder.Append("><tr><td align=\"center\" ");
        builder.Append(SurfaceAttributes(CanvasColor, "padding:0;"));
        builder.Append("><table role=\"presentation\" class=\"email-container\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" ");
        builder.Append(SurfaceAttributes(CardColor, "width:600px;max-width:600px;border:1px solid #3A3228;"));
        builder.Append("><tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:24px 32px 8px 32px;"));
        builder.Append('>');
        builder.Append(headerLogoHtml);
        builder.Append("</td></tr>");
        builder.Append(heroSectionHtml);
        builder.Append("<tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:24px 32px 8px 32px;"));
        builder.Append('>');
        builder.Append(BuildPasswordResetBadgeHtml());
        builder.Append("<h1 style=\"margin:0 0 12px 0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:24px;line-height:32px;font-weight:700;color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";text-align:center;\">");
        builder.Append(copy.Title);
        builder.Append("</h1><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:15px;line-height:24px;color:");
        builder.Append(SecondaryTextColor);
        builder.Append(";text-align:center;\">");
        builder.Append(copy.BodyHtml);
        builder.Append("</p></td></tr>");
        builder.Append(BuildBulletproofCtaRowHtml(encodedResetUrl, copy));
        builder.Append(MovieCaveEmailCtaFallback.BuildVisibleFallbackRowHtml(resetUrl, copy.FallbackPromptHtml));
        builder.Append("<tr><td class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:12px 32px 28px 32px;"));
        builder.Append("><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:13px;line-height:22px;color:#AFA79B;text-align:center;\">");
        builder.Append(copy.SafetyHtml);
        builder.Append("</p></td></tr>");
        builder.Append(BuildFeatureRowHtml(copy));
        builder.Append("<tr><td align=\"center\" class=\"section-padding\" ");
        builder.Append(SurfaceAttributes("#0D0D10", "padding:18px 32px 24px 32px;border-top:1px solid #3A3228;"));
        builder.Append("><p style=\"margin:0 0 4px 0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:11px;letter-spacing:0.24em;text-transform:uppercase;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">Movie Cave</p><p style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:12px;line-height:20px;color:#AFA79B;\">");
        builder.Append(copy.FooterTagline);
        builder.Append("</p></td></tr>");
        builder.Append("</table></td></tr></table></body></html>");

        return builder.ToString();
    }

    private static PasswordResetEmailCopy GetCopy(string contentLocale)
    {
        if (ContentLocaleResolver.RequiresLocalization(
                ContentLocaleResolver.ResolveFromAcceptLanguage(contentLocale)))
        {
            return TurkishCopy;
        }

        return EnglishCopy;
    }

    private static string BuildHeaderLogoHtml(string? logoImageUrl)
    {
        if (!string.IsNullOrWhiteSpace(logoImageUrl) &&
            Uri.TryCreate(logoImageUrl.Trim(), UriKind.Absolute, out var logoUri) &&
            (logoUri.Scheme == Uri.UriSchemeHttps || logoUri.Scheme == Uri.UriSchemeHttp))
        {
            var encodedLogoUrl = WebUtility.HtmlEncode(logoUri.ToString());
            var builder = new StringBuilder();
            builder.Append("<img class=\"");
            builder.Append(HeaderLogoMarkerClass);
            builder.Append("\" src=\"");
            builder.Append(encodedLogoUrl);
            builder.Append("\" width=\"");
            builder.Append(HeaderLogoDisplayWidthPx);
            builder.Append("\" alt=\"Movie Cave\" style=\"display:block;width:");
            builder.Append(HeaderLogoDisplayWidthPx);
            builder.Append("px;max-width:");
            builder.Append(HeaderLogoDisplayWidthPx);
            builder.Append("px;height:auto;border:0;\" />");
            return builder.ToString();
        }

        var fallback = new StringBuilder();
        fallback.Append("<p style=\"margin:0;font-family:");
        fallback.Append(SansFontStack);
        fallback.Append(";font-size:12px;letter-spacing:0.32em;text-transform:uppercase;color:");
        fallback.Append(GoldAccentColor);
        fallback.Append(";font-weight:700;\">Movie Cave</p>");
        return fallback.ToString();
    }

    private static string BuildHeroSectionHtml(string? heroImageUrl, PasswordResetEmailCopy copy)
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
            builder.Append(BuildHeroHeadlineHtml(copy));
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
        fallbackHero.Append(BuildHeroHeadlineHtml(copy));
        fallbackHero.Append("</td></tr></table></td></tr>");
        return fallbackHero.ToString();
    }

    private static string BuildHeroHeadlineHtml(PasswordResetEmailCopy copy)
    {
        var builder = new StringBuilder();
        builder.Append("<p class=\"hero-headline\" style=\"margin:0;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:20px;line-height:28px;font-weight:700;color:");
        builder.Append(PrimaryTextColor);
        builder.Append(";text-align:center;\">");
        builder.Append(copy.HeroHeadline);
        builder.Append("</p>");
        return builder.ToString();
    }

    private static string BuildPasswordResetBadgeHtml()
    {
        var builder = new StringBuilder();
        builder.Append("<table role=\"presentation\" class=\"");
        builder.Append(PasswordResetBadgeMarkerClass);
        builder.Append("\" width=\"48\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"width:48px;height:48px;margin:0 auto 16px auto;\"><tr><td align=\"center\" valign=\"middle\" width=\"48\" height=\"48\" bgcolor=\"#1A1712\" style=\"width:48px;height:48px;min-width:48px;max-width:48px;background-color:#1A1712;border:1px solid ");
        builder.Append(GoldAccentColor);
        builder.Append(";border-radius:24px;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:20px;line-height:48px;color:");
        builder.Append(GoldAccentColor);
        builder.Append(";font-weight:700;\">&#128274;</td></tr></table>");
        return builder.ToString();
    }

    private static string BuildBulletproofCtaRowHtml(string encodedResetUrl, PasswordResetEmailCopy copy)
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
        builder.Append(encodedResetUrl);
        builder.Append("\" target=\"_blank\" style=\"display:block;width:100%;box-sizing:border-box;padding:16px 24px;text-align:center;font-family:");
        builder.Append(SansFontStack);
        builder.Append(";font-size:16px;line-height:20px;font-weight:700;color:");
        builder.Append(CtaTextColor);
        builder.Append(" !important;text-decoration:none !important;\">");
        builder.Append(copy.CtaHtml);
        builder.Append("</a></td></tr></table></td></tr>");
        return builder.ToString();
    }

    private static string BuildFeatureRowHtml(PasswordResetEmailCopy copy)
    {
        var builder = new StringBuilder();
        builder.Append("<tr><td class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(CardColor, "padding:0 20px 24px 20px;"));
        builder.Append("><table role=\"presentation\" class=\"");
        builder.Append(FeatureRowMarkerClass);
        builder.Append("\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;\"><tr>");
        builder.Append(FeatureColumn(copy.Feature1Label, copy.Feature1TextHtml));
        builder.Append(FeatureColumn(copy.Feature2Label, copy.Feature2TextHtml));
        builder.Append(FeatureColumn(copy.Feature3Label, copy.Feature3TextHtml));
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

    private sealed record PasswordResetEmailCopy(
        string HtmlLang,
        string Subject,
        string Preheader,
        string HeroHeadline,
        string Title,
        string BodyPlain,
        string BodyHtml,
        string PlainTextCtaLabel,
        string CtaHtml,
        string FallbackPromptHtml,
        string SafetyHtml,
        string Feature1Label,
        string Feature1PlainText,
        string Feature1TextHtml,
        string Feature2Label,
        string Feature2PlainText,
        string Feature2TextHtml,
        string Feature3Label,
        string Feature3PlainText,
        string Feature3TextHtml,
        string FooterTagline,
        string Safety);

    private static readonly PasswordResetEmailCopy EnglishCopy = new(
        HtmlLang: "en",
        Subject: Subject,
        Preheader: "Create a new password for your Movie Cave account.",
        HeroHeadline: "Let's get you back in.",
        Title: "Reset your password",
        BodyPlain: "We received a request to reset your Movie Cave password. Use the link below to choose a new password.",
        BodyHtml: "We received a request to reset your Movie Cave password. Use the button below to choose a new password.",
        PlainTextCtaLabel: "Reset your password:",
        CtaHtml: "Reset Password &rarr;",
        FallbackPromptHtml: "If the button doesn&apos;t work, open this link in your browser:",
        SafetyHtml: "If you didn&apos;t request a password reset, you can safely ignore this email. Your password won&apos;t change.",
        Feature1Label: "Secure",
        Feature1PlainText: "Account Protection",
        Feature1TextHtml: "Account Protection",
        Feature2Label: "Quick",
        Feature2PlainText: "One-Time Link",
        Feature2TextHtml: "One-Time Link",
        Feature3Label: "Back",
        Feature3PlainText: "To Your Watchlist",
        Feature3TextHtml: "To Your Watchlist",
        FooterTagline: "Your cinematic home for movies and TV.",
        Safety: "If you didn't request a password reset, you can safely ignore this email. Your password won't change.");

    private static readonly PasswordResetEmailCopy TurkishCopy = new(
        HtmlLang: "tr",
        Subject: "Movie Cave şifreni sıfırla",
        Preheader: "Movie Cave hesabın için yeni bir şifre oluştur.",
        HeroHeadline: "Hesabına tekrar erişelim.",
        Title: "Şifreni sıfırla",
        BodyPlain: "Movie Cave şifreni sıfırlamak için bir istek aldık. Yeni bir şifre belirlemek için aşağıdaki bağlantıyı kullan.",
        BodyHtml: "Movie Cave şifreni sıfırlamak için bir istek aldık. Yeni bir şifre belirlemek için aşağıdaki düğmeyi kullan.",
        PlainTextCtaLabel: "Şifreni sıfırla:",
        CtaHtml: "Şifremi Sıfırla &rarr;",
        FallbackPromptHtml: "Düğme çalışmıyorsa bu bağlantıyı tarayıcında aç:",
        SafetyHtml: "Şifre sıfırlama talebinde bulunmadıysan bu e-postayı güvenle yok sayabilirsin. Şifren değişmeyecek.",
        Feature1Label: "GÜVENLİ",
        Feature1PlainText: "Hesap Koruması",
        Feature1TextHtml: "Hesap Koruması",
        Feature2Label: "HIZLI",
        Feature2PlainText: "Tek Kullanımlık Bağlantı",
        Feature2TextHtml: "Tek Kullanımlık Bağlantı",
        Feature3Label: "GERİ DÖN",
        Feature3PlainText: "İzleme Listen",
        Feature3TextHtml: "İzleme Listen",
        FooterTagline: "Film ve dizi için sinematik evin.",
        Safety: "Şifre sıfırlama talebinde bulunmadıysan bu e-postayı güvenle yok sayabilirsin. Şifren değişmeyecek.");
}
