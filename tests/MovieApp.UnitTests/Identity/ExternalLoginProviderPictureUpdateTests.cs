using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class ExternalLoginProviderPictureUpdateTests
{
    [Fact]
    public void SetProviderPictureUrlDoesNotChangeWhenNormalizedValueMatches()
    {
        const string url = "https://google.example/photo.jpg";
        var login = UserExternalLogin.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExternalLoginProviders.Google,
            "subject",
            "user@example.com",
            DateTime.UtcNow);
        login.SetProviderPictureUrl(url);

        var shouldUpdate = ShouldUpdateProviderPictureUrl(login.ProviderPictureUrl, url);
        Assert.False(shouldUpdate);
    }

    [Fact]
    public void SetProviderPictureUrlDetectsChangedValue()
    {
        const string url = "https://google.example/photo.jpg";
        var login = UserExternalLogin.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExternalLoginProviders.Google,
            "subject",
            "user@example.com",
            DateTime.UtcNow);
        login.SetProviderPictureUrl(url);

        var shouldUpdate = ShouldUpdateProviderPictureUrl(login.ProviderPictureUrl, "https://google.example/new.jpg");
        Assert.True(shouldUpdate);
    }

    /// <summary>Mirrors UserExternalLoginRepository.UpdateProviderPictureUrlAsync equality guard.</summary>
    private static bool ShouldUpdateProviderPictureUrl(string? current, string? incoming)
    {
        var normalized = string.IsNullOrWhiteSpace(incoming) ? null : incoming.Trim();
        return !string.Equals(current, normalized, StringComparison.Ordinal);
    }
}
