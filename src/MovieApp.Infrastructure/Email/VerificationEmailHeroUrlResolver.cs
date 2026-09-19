namespace MovieApp.Infrastructure.Email;

public static class VerificationEmailHeroUrlResolver
{
    public const string DefaultHeroImagePath = "/email-assets/verification-hero.jpg";

    public static string? Resolve(string? heroImageUrl, string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(heroImageUrl))
        {
            return null;
        }

        var trimmed = heroImageUrl.Trim();
        if (TryCreateAllowedUrl(trimmed, out var absolute))
        {
            return absolute;
        }

        if (!trimmed.StartsWith('/'))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return null;
        }

        var combined = publicBaseUrl.Trim().TrimEnd('/') + trimmed;
        return TryCreateAllowedUrl(combined, out var resolved) ? resolved : null;
    }

    private static bool TryCreateAllowedUrl(string value, out string url)
    {
        url = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        url = uri.ToString();
        return true;
    }
}
