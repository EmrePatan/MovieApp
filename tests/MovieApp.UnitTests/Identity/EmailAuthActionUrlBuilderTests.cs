using MovieApp.Application.Services.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class EmailAuthActionUrlBuilderTests
{
    private const string ProductionVerifyBaseUrl = "https://moviecaveapp.com/auth/verify-email";
    private const string ProductionResetBaseUrl = "https://moviecaveapp.com/auth/reset-password";

    [Theory]
    [InlineData(ProductionVerifyBaseUrl, "raw-token", "https://moviecaveapp.com/auth/verify-email?token=raw-token")]
    [InlineData(ProductionResetBaseUrl, "abc+def/ghi", "https://moviecaveapp.com/auth/reset-password?token=abc%2Bdef%2Fghi")]
    [InlineData("movieapp://verify-email", "abc123", "movieapp://verify-email?token=abc123")]
    public void Build_AppendsEscapedTokenQuery(string baseUrl, string rawToken, string expected)
    {
        var actual = EmailAuthActionUrlBuilder.Build(baseUrl, rawToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Build_ThrowsWhenBaseUrlMissing()
    {
        Assert.Throws<InvalidOperationException>(() => EmailAuthActionUrlBuilder.Build("   ", "token"));
    }

    [Theory]
    [InlineData("https://moviecaveapp.com/auth/verify-email", true)]
    [InlineData("http://localhost/auth/verify-email", false)]
    [InlineData("movieapp://verify-email", false)]
    [InlineData("not-a-url", false)]
    public void IsProductionSafeAbsoluteUrl_RequiresHttpsAbsoluteUrl(string baseUrl, bool expected)
    {
        Assert.Equal(expected, EmailAuthActionUrlBuilder.IsProductionSafeAbsoluteUrl(baseUrl));
    }
}
