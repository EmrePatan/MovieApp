using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public static class UserAvatarResolver
{
    public static UserAvatarPresentation Resolve(
        UserAvatarSources sources,
        Func<string?, string?> resolveCustomUrl)
    {
        var customUrl = resolveCustomUrl(sources.CustomAvatarStorageKey);
        if (!string.IsNullOrWhiteSpace(customUrl))
        {
            return new UserAvatarPresentation(
                customUrl,
                sources.GoogleProviderPictureUrl,
                customUrl,
                UserAvatarKind.Custom);
        }

        if (!string.IsNullOrWhiteSpace(sources.GoogleProviderPictureUrl))
        {
            return new UserAvatarPresentation(
                null,
                sources.GoogleProviderPictureUrl,
                sources.GoogleProviderPictureUrl,
                UserAvatarKind.Provider);
        }

        return new UserAvatarPresentation(null, null, null, UserAvatarKind.Initials);
    }

    public static string? ResolveEffectiveAvatarUrl(
        UserAvatarSources sources,
        Func<string?, string?> resolveCustomUrl)
    {
        return Resolve(sources, resolveCustomUrl).EffectiveAvatarUrl;
    }
}
