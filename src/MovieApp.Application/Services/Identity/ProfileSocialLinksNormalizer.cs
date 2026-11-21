using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public static class ProfileSocialLinksNormalizer
{
    public static ProfileSocialLinksResult? Normalize(ProfileSocialLinksResult? links)
    {
        if (links is null)
        {
            return null;
        }

        return new ProfileSocialLinksResult(
            NormalizeHandle(links.X),
            NormalizeHandle(links.Instagram),
            NormalizeHandle(links.Snapchat),
            NormalizeHandle(links.Tiktok),
            NormalizeHandle(links.Youtube),
            NormalizeHandle(links.Twitch),
            NormalizeHandle(links.Threads),
            NormalizeWebsite(links.Website));
    }

    private static string? NormalizeHandle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.StartsWith('@'))
        {
            value = value[1..];
        }

        value = value.Split(['/', '?', '#'], 2)[0].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? NormalizeWebsite(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.ToString();
    }
}
