using System.Net;
using System.Text;

namespace MovieApp.Infrastructure.Email;

internal static class MovieCaveEmailCtaFallback
{
    public static string BuildVisibleFallbackRowHtml(string actionUrl, string promptHtml)
    {
        var encodedActionUrl = WebUtility.HtmlEncode(actionUrl);
        var builder = new StringBuilder();
        builder.Append("<tr><td class=\"section-padding\" ");
        builder.Append(SurfaceAttributes(
            MovieCaveVerificationEmailContent.CardColor,
            "padding:0 32px 16px 32px;"));
        builder.Append("><p style=\"margin:0 0 8px 0;font-family:");
        builder.Append(MovieCaveVerificationEmailContent.SansFontStack);
        builder.Append(";font-size:13px;line-height:20px;color:#AFA79B;text-align:center;\">");
        builder.Append(promptHtml);
        builder.Append("</p><p style=\"margin:0;font-family:");
        builder.Append(MovieCaveVerificationEmailContent.SansFontStack);
        builder.Append(";font-size:12px;line-height:18px;text-align:center;word-break:break-all;\">");
        builder.Append("<a class=\"cta-button-link\" href=\"");
        builder.Append(encodedActionUrl);
        builder.Append("\" target=\"_blank\" style=\"color:");
        builder.Append(MovieCaveVerificationEmailContent.GoldAccentColor);
        builder.Append(" !important;text-decoration:underline !important;\">");
        builder.Append(encodedActionUrl);
        builder.Append("</a></p></td></tr>");
        return builder.ToString();
    }

    private static string SurfaceAttributes(string color, string cssDeclarations) =>
        "bgcolor=\"" + color + "\" style=\"background-color:" + color + ";background-image:linear-gradient(" + color + "," + color + ");" + cssDeclarations + "\"";
}
