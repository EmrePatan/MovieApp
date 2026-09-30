using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class UserAvatarResolverTests
{
    private const string PublicBase = "https://avatars.moviecaveapp.com";

    [Fact]
    public void ResolvePrefersCustomOverProvider()
    {
        var sources = new UserAvatarSources(
            Guid.NewGuid(),
            "avatars/user/key.webp",
            "https://google.example/photo.jpg");

        var presentation = UserAvatarResolver.Resolve(sources, key => BuildUrl(key));

        Assert.Equal(UserAvatarKind.Custom, presentation.AvatarKind);
        Assert.Equal($"{PublicBase}/avatars/user/key.webp", presentation.CustomAvatarUrl);
        Assert.Equal($"{PublicBase}/avatars/user/key.webp", presentation.EffectiveAvatarUrl);
        Assert.Equal("https://google.example/photo.jpg", presentation.ProviderAvatarUrl);
    }

    [Fact]
    public void ResolveUsesProviderWhenNoCustom()
    {
        var sources = new UserAvatarSources(Guid.NewGuid(), null, "https://google.example/photo.jpg");

        var presentation = UserAvatarResolver.Resolve(sources, BuildUrl);

        Assert.Equal(UserAvatarKind.Provider, presentation.AvatarKind);
        Assert.Null(presentation.CustomAvatarUrl);
        Assert.Equal("https://google.example/photo.jpg", presentation.EffectiveAvatarUrl);
    }

    [Fact]
    public void ResolveUsesInitialsWhenNoSources()
    {
        var sources = new UserAvatarSources(Guid.NewGuid(), null, null);

        var presentation = UserAvatarResolver.Resolve(sources, BuildUrl);

        Assert.Equal(UserAvatarKind.Initials, presentation.AvatarKind);
        Assert.Null(presentation.EffectiveAvatarUrl);
    }

    private static string? BuildUrl(string? key) => UserAvatarUrlBuilder.BuildPublicUrl(PublicBase, key);
}
