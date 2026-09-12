using MovieApp.Application.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class PasswordResetTokenHasherTests
{
    [Fact]
    public void HashTokenReturnsDeterministicUppercaseHexDigest()
    {
        const string rawToken = "sample-reset-token";

        var first = PasswordResetTokenHasher.HashToken(rawToken);
        var second = PasswordResetTokenHasher.HashToken(rawToken);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(rawToken, first, StringComparison.Ordinal);
    }

    [Fact]
    public void HashTokenThrowsForBlankInput()
    {
        Assert.Throws<ArgumentException>(() => PasswordResetTokenHasher.HashToken(" "));
    }
}
