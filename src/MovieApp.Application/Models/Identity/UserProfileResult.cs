namespace MovieApp.Application.Models.Identity;

public sealed record ProfileSocialLinksResult(
    string? X = null,
    string? Instagram = null,
    string? Snapchat = null,
    string? Tiktok = null,
    string? Youtube = null,
    string? Twitch = null,
    string? Threads = null,
    string? Website = null)
{
    public static ProfileSocialLinksResult? FromUser(
        string? x,
        string? instagram,
        string? snapchat,
        string? tiktok,
        string? youtube,
        string? twitch,
        string? threads,
        string? website)
    {
        if (x is null
            && instagram is null
            && snapchat is null
            && tiktok is null
            && youtube is null
            && twitch is null
            && threads is null
            && website is null)
        {
            return null;
        }

        return new ProfileSocialLinksResult(x, instagram, snapchat, tiktok, youtube, twitch, threads, website);
    }

    public bool HasAnyValue =>
        !string.IsNullOrWhiteSpace(X)
        || !string.IsNullOrWhiteSpace(Instagram)
        || !string.IsNullOrWhiteSpace(Snapchat)
        || !string.IsNullOrWhiteSpace(Tiktok)
        || !string.IsNullOrWhiteSpace(Youtube)
        || !string.IsNullOrWhiteSpace(Twitch)
        || !string.IsNullOrWhiteSpace(Threads)
        || !string.IsNullOrWhiteSpace(Website);
}

public sealed record UserProfileResult(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    DateTime CreatedAt,
    bool HasPassword,
    IReadOnlyList<string> LinkedProviders,
    string? PendingEmail = null,
    string? CustomAvatarUrl = null,
    string? ProviderAvatarUrl = null,
    string? EffectiveAvatarUrl = null,
    UserAvatarKind AvatarKind = UserAvatarKind.Initials,
    string? Bio = null,
    ProfileSocialLinksResult? SocialLinks = null);
