using System.Net;
using System.Text;
using MovieApp.Application.Services.Localization;

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
    internal const string HeaderLogoMarkerClass = "movie-cave-header-logo";
    internal const string EnvelopeBadgeMarkerClass = "movie-cave-envelope-badge";
    internal const string CtaButtonMarkerClass = "movie-cave-cta-button";
    internal const string FeatureRowMarkerClass = "movie-cave-feature-row";
    internal const int HeaderLogoDisplayWidthPx = 175;

    public static string GetSubject(string contentLocale) =>
        GetCopy(contentLocale).Subject;

    public static string BuildPlainText(
        string verifyUrl,
        string contentLocale = ContentLocaleResolver.EnglishUnitedStates)
    {
        var copy = GetCopy(contentLocale);
        return $"""
            MOVIE CAVE

            {copy.HeroHeadline}

            {copy.Title}

            {copy.BodyPlain}

            {copy.PlainTextCtaLabel}
            {verifyUrl}

            {copy.Safety}

            {copy.Feature1Label} — {copy.Feature1PlainText}
            {copy.Feature2Label} — {copy.Feature2PlainText}
            {copy.Feature3Label} — {copy.Feature3PlainText}

            Movie Cave
            """;
    }

    public static string BuildHtml(
        string verifyUrl,
        string? heroImageUrl,
        string? logoImageUrl = null,
        string contentLocale = ContentLocaleResolver.EnglishUnitedStates)
    {
        var copy = GetCopy(contentLocale);
        var encodedVerifyUrl = WebUtility.HtmlEncode(verifyUrl);
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
        builder.Append(BuildEnvelopeBadgeHtml());
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
        builder.Append(BuildBulletproofCtaRowHtml(encodedVerifyUrl, copy));
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

    private static VerificationEmailCopy GetCopy(string contentLocale)
    {
        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        return normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => TurkishCopy,
            ContentLocaleResolver.SpanishSpain => SpanishCopy,
            ContentLocaleResolver.GermanGermany => GermanCopy,
            ContentLocaleResolver.FrenchFrance => FrenchCopy,
            ContentLocaleResolver.ItalianItaly => ItalianCopy,
            ContentLocaleResolver.PortugueseBrazil => PortugueseBrazilCopy,
            _ => EnglishCopy
        };
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

    private static string BuildHeroSectionHtml(string? heroImageUrl, VerificationEmailCopy copy)
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

    private static string BuildHeroHeadlineHtml(VerificationEmailCopy copy)
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

    private static string BuildBulletproofCtaRowHtml(string encodedVerifyUrl, VerificationEmailCopy copy)
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
        builder.Append(" !important;text-decoration:none !important;\">");
        builder.Append(copy.CtaHtml);
        builder.Append("</a></td></tr></table></td></tr>");
        return builder.ToString();
    }

    private static string BuildFeatureRowHtml(VerificationEmailCopy copy)
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

    private sealed record VerificationEmailCopy(
        string HtmlLang,
        string Subject,
        string Preheader,
        string HeroHeadline,
        string Title,
        string BodyPlain,
        string BodyHtml,
        string PlainTextCtaLabel,
        string CtaHtml,
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

    private static readonly VerificationEmailCopy EnglishCopy = new(
        HtmlLang: "en",
        Subject: Subject,
        Preheader: "One more step to the good stuff. Verify your Movie Cave email address.",
        HeroHeadline: "One more step to the good stuff.",
        Title: "Verify your email address",
        BodyPlain: "Welcome to Movie Cave. Confirm your email to unlock your watchlist, discover movies and TV shows, and start finding your next favorite.",
        BodyHtml: "Welcome to Movie Cave. Confirm your email to unlock your watchlist, discover movies and TV shows, and start finding your next favorite.",
        PlainTextCtaLabel: "Verify your email address:",
        CtaHtml: "Verify Email Address &rarr;",
        SafetyHtml: "If you didn&apos;t create a Movie Cave account, you can safely ignore this email.",
        Feature1Label: "Discover",
        Feature1PlainText: "Movies & TV Shows",
        Feature1TextHtml: "Movies &amp; TV Shows",
        Feature2Label: "Save",
        Feature2PlainText: "Your Watchlist",
        Feature2TextHtml: "Your Watchlist",
        Feature3Label: "Enjoy",
        Feature3PlainText: "Your Next Favorite",
        Feature3TextHtml: "Your Next Favorite",
        FooterTagline: "Your cinematic home for movies and TV.",
        Safety: "If you didn't create a Movie Cave account, you can safely ignore this email.");

    private static readonly VerificationEmailCopy TurkishCopy = new(
        HtmlLang: "tr",
        Subject: "Movie Cave e-posta adresini doğrula",
        Preheader: "Güzel şeylere sadece bir adım kaldı. Movie Cave e-posta adresini doğrula.",
        HeroHeadline: "Güzel şeylere sadece bir adım kaldı.",
        Title: "E-posta adresini doğrula",
        BodyPlain: "Movie Cave'e hoş geldin. İzleme listeni kullanmak, film ve dizileri keşfetmek ve sıradaki favorini bulmak için e-posta adresini doğrula.",
        BodyHtml: "Movie Cave&apos;e hoş geldin. İzleme listeni kullanmak, film ve dizileri keşfetmek ve sıradaki favorini bulmak için e-posta adresini doğrula.",
        PlainTextCtaLabel: "E-posta Adresimi Doğrula:",
        CtaHtml: "E-posta Adresimi Doğrula &rarr;",
        SafetyHtml: "Bu Movie Cave hesabını sen oluşturmadıysan bu e-postayı güvenle yok sayabilirsin.",
        Feature1Label: "KEŞFET",
        Feature1PlainText: "Film & Diziler",
        Feature1TextHtml: "Film &amp; Diziler",
        Feature2Label: "KAYDET",
        Feature2PlainText: "İzleme Listen",
        Feature2TextHtml: "İzleme Listen",
        Feature3Label: "KEYFİNİ ÇIKAR",
        Feature3PlainText: "Sıradaki Favorin",
        Feature3TextHtml: "Sıradaki Favorin",
        FooterTagline: "Film ve dizi için sinematik evin.",
        Safety: "Bu Movie Cave hesabını sen oluşturmadıysan bu e-postayı güvenle yok sayabilirsin.");

    private static readonly VerificationEmailCopy SpanishCopy = new(
        HtmlLang: "es",
        Subject: "Verifica tu dirección de correo de Movie Cave",
        Preheader: "Solo un paso más para lo bueno. Verifica tu dirección de correo de Movie Cave.",
        HeroHeadline: "Solo un paso más para lo bueno.",
        Title: "Verifica tu dirección de correo",
        BodyPlain: "Bienvenido a Movie Cave. Confirma tu correo para desbloquear tu lista, descubrir películas y series, y empezar a encontrar tu próximo favorito.",
        BodyHtml: "Bienvenido a Movie Cave. Confirma tu correo para desbloquear tu lista, descubrir películas y series, y empezar a encontrar tu próximo favorito.",
        PlainTextCtaLabel: "Verificar mi dirección de correo:",
        CtaHtml: "Verificar dirección de correo &rarr;",
        SafetyHtml: "Si no creaste una cuenta de Movie Cave, puedes ignorar este correo con tranquilidad.",
        Feature1Label: "DESCUBRE",
        Feature1PlainText: "Películas y series",
        Feature1TextHtml: "Películas y series",
        Feature2Label: "GUARDA",
        Feature2PlainText: "Tu lista",
        Feature2TextHtml: "Tu lista",
        Feature3Label: "DISFRUTA",
        Feature3PlainText: "Tu próximo favorito",
        Feature3TextHtml: "Tu próximo favorito",
        FooterTagline: "Tu hogar cinematográfico para películas y series.",
        Safety: "Si no creaste una cuenta de Movie Cave, puedes ignorar este correo con tranquilidad.");

    private static readonly VerificationEmailCopy GermanCopy = new(
        HtmlLang: "de",
        Subject: "Bestätige deine Movie-Cave-E-Mail-Adresse",
        Preheader: "Nur noch ein Schritt. Bestätige deine Movie-Cave-E-Mail-Adresse.",
        HeroHeadline: "Nur noch ein Schritt.",
        Title: "E-Mail-Adresse bestätigen",
        BodyPlain: "Willkommen bei Movie Cave. Bestätige deine E-Mail, um deine Merkliste freizuschalten, Filme und Serien zu entdecken und deinen nächsten Favoriten zu finden.",
        BodyHtml: "Willkommen bei Movie Cave. Bestätige deine E-Mail, um deine Merkliste freizuschalten, Filme und Serien zu entdecken und deinen nächsten Favoriten zu finden.",
        PlainTextCtaLabel: "E-Mail-Adresse bestätigen:",
        CtaHtml: "E-Mail-Adresse bestätigen &rarr;",
        SafetyHtml: "Wenn du kein Movie-Cave-Konto erstellt hast, kannst du diese E-Mail ignorieren.",
        Feature1Label: "ENTDECKEN",
        Feature1PlainText: "Filme & Serien",
        Feature1TextHtml: "Filme &amp; Serien",
        Feature2Label: "SPEICHERN",
        Feature2PlainText: "Deine Merkliste",
        Feature2TextHtml: "Deine Merkliste",
        Feature3Label: "GENIESSEN",
        Feature3PlainText: "Dein nächster Favorit",
        Feature3TextHtml: "Dein nächster Favorit",
        FooterTagline: "Dein filmisches Zuhause für Filme und Serien.",
        Safety: "Wenn du kein Movie-Cave-Konto erstellt hast, kannst du diese E-Mail ignorieren.");

    private static readonly VerificationEmailCopy FrenchCopy = new(
        HtmlLang: "fr",
        Subject: "Vérifiez votre adresse e-mail Movie Cave",
        Preheader: "Plus qu'une étape. Vérifiez votre adresse e-mail Movie Cave.",
        HeroHeadline: "Plus qu'une étape.",
        Title: "Vérifiez votre adresse e-mail",
        BodyPlain: "Bienvenue sur Movie Cave. Confirmez votre e-mail pour débloquer votre liste, découvrir des films et séries et trouver votre prochain favori.",
        BodyHtml: "Bienvenue sur Movie Cave. Confirmez votre e-mail pour débloquer votre liste, découvrir des films et séries et trouver votre prochain favori.",
        PlainTextCtaLabel: "Vérifier mon adresse e-mail :",
        CtaHtml: "Vérifier l'adresse e-mail &rarr;",
        SafetyHtml: "Si vous n'avez pas créé de compte Movie Cave, vous pouvez ignorer cet e-mail.",
        Feature1Label: "DÉCOUVRIR",
        Feature1PlainText: "Films et séries",
        Feature1TextHtml: "Films et séries",
        Feature2Label: "ENREGISTRER",
        Feature2PlainText: "Votre liste",
        Feature2TextHtml: "Votre liste",
        Feature3Label: "PROFITER",
        Feature3PlainText: "Votre prochain favori",
        Feature3TextHtml: "Votre prochain favori",
        FooterTagline: "Votre foyer cinéma pour films et séries.",
        Safety: "Si vous n'avez pas créé de compte Movie Cave, vous pouvez ignorer cet e-mail.");

    private static readonly VerificationEmailCopy ItalianCopy = new(
        HtmlLang: "it",
        Subject: "Verifica il tuo indirizzo e-mail Movie Cave",
        Preheader: "Ancora un passo. Verifica il tuo indirizzo e-mail Movie Cave.",
        HeroHeadline: "Ancora un passo.",
        Title: "Verifica il tuo indirizzo e-mail",
        BodyPlain: "Benvenuto su Movie Cave. Conferma la tua e-mail per sbloccare la watchlist, scoprire film e serie e trovare il tuo prossimo preferito.",
        BodyHtml: "Benvenuto su Movie Cave. Conferma la tua e-mail per sbloccare la watchlist, scoprire film e serie e trovare il tuo prossimo preferito.",
        PlainTextCtaLabel: "Verifica indirizzo e-mail:",
        CtaHtml: "Verifica indirizzo e-mail &rarr;",
        SafetyHtml: "Se non hai creato un account Movie Cave, puoi ignorare questa e-mail.",
        Feature1Label: "SCOPRI",
        Feature1PlainText: "Film e serie",
        Feature1TextHtml: "Film e serie",
        Feature2Label: "SALVA",
        Feature2PlainText: "La tua watchlist",
        Feature2TextHtml: "La tua watchlist",
        Feature3Label: "GODITI",
        Feature3PlainText: "Il tuo prossimo preferito",
        Feature3TextHtml: "Il tuo prossimo preferito",
        FooterTagline: "La tua casa cinematografica per film e serie.",
        Safety: "Se non hai creato un account Movie Cave, puoi ignorare questa e-mail.");

    private static readonly VerificationEmailCopy PortugueseBrazilCopy = new(
        HtmlLang: "pt-BR",
        Subject: "Verifique seu e-mail do Movie Cave",
        Preheader: "Falta só um passo. Verifique seu e-mail do Movie Cave.",
        HeroHeadline: "Falta só um passo.",
        Title: "Verifique seu e-mail",
        BodyPlain: "Bem-vindo ao Movie Cave. Confirme seu e-mail para desbloquear sua lista, descobrir filmes e séries e encontrar seu próximo favorito.",
        BodyHtml: "Bem-vindo ao Movie Cave. Confirme seu e-mail para desbloquear sua lista, descobrir filmes e séries e encontrar seu próximo favorito.",
        PlainTextCtaLabel: "Verificar e-mail:",
        CtaHtml: "Verificar e-mail &rarr;",
        SafetyHtml: "Se você não criou uma conta Movie Cave, pode ignorar este e-mail.",
        Feature1Label: "DESCUBRA",
        Feature1PlainText: "Filmes e séries",
        Feature1TextHtml: "Filmes e séries",
        Feature2Label: "SALVE",
        Feature2PlainText: "Sua lista",
        Feature2TextHtml: "Sua lista",
        Feature3Label: "APROVEITE",
        Feature3PlainText: "Seu próximo favorito",
        Feature3TextHtml: "Seu próximo favorito",
        FooterTagline: "Seu lar cinematográfico para filmes e séries.",
        Safety: "Se você não criou uma conta Movie Cave, pode ignorar este e-mail.");
}
