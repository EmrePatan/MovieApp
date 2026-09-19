using System.Net;
using System.Text;

namespace MovieApp.Infrastructure.Email;

public static class MovieCaveVerificationEmailContent
{
    public const string Subject = "Verify your Movie Cave email address";

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

        var builder = new StringBuilder(8_192);
        builder.Append("""
            <!DOCTYPE html>
            <html lang="en" xmlns="http://www.w3.org/1999/xhtml" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
            <head>
              <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <meta name="color-scheme" content="dark" />
              <meta name="supported-color-schemes" content="dark" />
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
                body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
                table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
                img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
                body { margin: 0 !important; padding: 0 !important; width: 100% !important; }
                @media only screen and (max-width: 620px) {
                  .email-container { width: 100% !important; }
                  .feature-column { display: block !important; width: 100% !important; padding: 0 0 16px 0 !important; }
                  .hero-headline { font-size: 24px !important; line-height: 32px !important; }
                  .cta-button { display: block !important; width: 100% !important; box-sizing: border-box !important; }
                }
              </style>
            </head>
            <body style="margin:0;padding:0;background-color:#08080a;font-family:Georgia,'Times New Roman',serif;">
              <div style="display:none;max-height:0;overflow:hidden;mso-hide:all;">
                One more step to the good stuff. Verify your Movie Cave email address.
              </div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#08080a;">
                <tr>
                  <td align="center" style="padding:32px 16px;">
                    <table role="presentation" class="email-container" width="600" cellpadding="0" cellspacing="0" border="0" style="width:600px;max-width:600px;background-color:#121216;border:1px solid #2a2418;border-radius:16px;overflow:hidden;">
                      <tr>
                        <td align="center" style="padding:28px 32px 12px 32px;background-color:#121216;">
                          <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:13px;letter-spacing:0.35em;text-transform:uppercase;color:#c8a24a;font-weight:700;">Movie Cave</p>
                        </td>
                      </tr>
            """);
        builder.Append(heroSectionHtml);
        builder.Append("""
                      <tr>
                        <td style="padding:32px 32px 8px 32px;background-color:#121216;">
                          <table role="presentation" width="56" height="56" cellpadding="0" cellspacing="0" border="0" style="margin:0 auto 20px auto;">
                            <tr>
                              <td align="center" valign="middle" width="56" height="56" style="width:56px;height:56px;background-color:#1c1810;border:1px solid #3a3020;border-radius:28px;font-size:24px;line-height:56px;color:#d4af37;font-family:Arial,Helvetica,sans-serif;">
                                &#9993;
                              </td>
                            </tr>
                          </table>
                          <h1 style="margin:0 0 16px 0;font-family:Arial,Helvetica,sans-serif;font-size:24px;line-height:32px;font-weight:700;color:#f4f0e8;text-align:center;">Verify your email address</h1>
                          <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:26px;color:#b8b2a8;text-align:center;">
                            Welcome to Movie Cave. Confirm your email to unlock your watchlist, discover movies and TV shows, and start finding your next favorite.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:8px 32px 28px 32px;background-color:#121216;">
                          <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td align="center" class="cta-button" style="border-radius:999px;background-color:#c8a24a;">
                                <a href="
            """);
        builder.Append(encodedVerifyUrl);
        builder.Append("""
            " style="display:inline-block;padding:16px 32px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:20px;font-weight:700;color:#121216;text-decoration:none;border-radius:999px;background-color:#c8a24a;">Verify Email Address &rarr;</a>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 32px 28px 32px;background-color:#121216;">
                          <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:22px;color:#8f887c;text-align:center;">
                            If you didn&apos;t create a Movie Cave account, you can safely ignore this email.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:0 24px 28px 24px;background-color:#121216;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td class="feature-column" width="33%" valign="top" style="width:33%;padding:0 8px;text-align:center;">
                                <p style="margin:0 0 6px 0;font-family:Arial,Helvetica,sans-serif;font-size:11px;letter-spacing:0.18em;text-transform:uppercase;color:#c8a24a;font-weight:700;">Discover</p>
                                <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#ddd6cb;">Movies &amp; TV Shows</p>
                              </td>
                              <td class="feature-column" width="34%" valign="top" style="width:34%;padding:0 8px;text-align:center;">
                                <p style="margin:0 0 6px 0;font-family:Arial,Helvetica,sans-serif;font-size:11px;letter-spacing:0.18em;text-transform:uppercase;color:#c8a24a;font-weight:700;">Save</p>
                                <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#ddd6cb;">Your Watchlist</p>
                              </td>
                              <td class="feature-column" width="33%" valign="top" style="width:33%;padding:0 8px;text-align:center;">
                                <p style="margin:0 0 6px 0;font-family:Arial,Helvetica,sans-serif;font-size:11px;letter-spacing:0.18em;text-transform:uppercase;color:#c8a24a;font-weight:700;">Enjoy</p>
                                <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#ddd6cb;">Your Next Favorite</p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:20px 32px 28px 32px;background-color:#0d0d10;border-top:1px solid #242018;">
                          <p style="margin:0 0 6px 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;letter-spacing:0.28em;text-transform:uppercase;color:#8a7a58;font-weight:700;">Movie Cave</p>
                          <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px;color:#6f6a62;">Your cinematic home for movies and TV.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """);

        return builder.ToString();
    }

    private static string BuildHeroSectionHtml(string? heroImageUrl)
    {
        if (!string.IsNullOrWhiteSpace(heroImageUrl) &&
            Uri.TryCreate(heroImageUrl.Trim(), UriKind.Absolute, out var heroUri) &&
            (heroUri.Scheme == Uri.UriSchemeHttps || heroUri.Scheme == Uri.UriSchemeHttp))
        {
            var encodedHeroUrl = WebUtility.HtmlEncode(heroUri.ToString());
            return
                """
                      <tr>
                        <td style="padding:0;background-color:#121216;">
                          <img src="
                """ +
                encodedHeroUrl +
                """
                " width="600" alt="Movie Cave cinematic hero" style="display:block;width:100%;max-width:600px;height:auto;border:0;" />
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px 32px 8px 32px;background-color:#121216;">
                          <p class="hero-headline" style="margin:0;font-family:Georgia,'Times New Roman',serif;font-size:28px;line-height:36px;font-weight:400;color:#f4f0e8;text-align:center;">One more step to the good stuff.</p>
                        </td>
                      </tr>
                """;
        }

        return """
                      <tr>
                        <td style="padding:0;background-color:#121216;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#1a1410;">
                            <tr>
                              <td height="180" style="height:180px;background-color:#1a1410;background:linear-gradient(135deg,#1a1410 0%,#2a2014 45%,#121216 100%);">
                                <p style="margin:0;padding:64px 32px 0 32px;font-family:Georgia,'Times New Roman',serif;font-size:28px;line-height:36px;color:#f4f0e8;text-align:center;">One more step to the good stuff.</p>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
            """;
    }
}
