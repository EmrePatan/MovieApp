using MovieApp.Application.Services.Videos;

namespace MovieApp.UnitTests.Videos;

public sealed class YouTubeKeyValidatorTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ")]
    [InlineData("abc123_-")]
    public void IsValidAcceptsNormalYouTubeKeys(string key)
    {
        Assert.True(YouTubeKeyValidator.IsValid(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/key")]
    [InlineData("watch?v=abc")]
    [InlineData("abc?foo=bar")]
    [InlineData("abc bar")]
    public void IsValidRejectsInvalidKeys(string? key)
    {
        Assert.False(YouTubeKeyValidator.IsValid(key));
    }
}
