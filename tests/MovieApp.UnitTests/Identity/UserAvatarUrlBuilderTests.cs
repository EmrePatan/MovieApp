using MovieApp.Application.Services.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class UserAvatarUrlBuilderTests
{
    [Fact]
    public void BuildPublicUrlJoinsBaseAndKeyWithoutDuplicateSlash()
    {
        var url = UserAvatarUrlBuilder.BuildPublicUrl(
            "https://avatars.moviecaveapp.com/",
            "/avatars/user/key.webp");

        Assert.Equal("https://avatars.moviecaveapp.com/avatars/user/key.webp", url);
    }
}
