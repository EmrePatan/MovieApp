using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Identity;
using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class SecureStringUtilitiesTests
{
    [Theory]
    [InlineData("SimplePassword1")]
    [InlineData("P@ss w0rd! with spaces")]
    [InlineData("unicode-ok-Angstrom-ae")]
    [InlineData("long-" + "abcdefghijklmnop" + "qrstuvwxyz0123456789!@#$%")]
    public void SecureStringRoundTripPreservesPassword(string password)
    {
        using var secure = SecureStringUtilities.CreateFromString(password);
        var plain = SecureStringUtilities.ToPlainString(secure);
        Assert.Equal(password, plain);
    }

    [Fact]
    public void HasherVerifiesPasswordFromSecureStringRoundTrip()
    {
        const string password = "Campaign! pass 2026";
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.HashPassword(password);

        using var secure = SecureStringUtilities.CreateFromString(password);
        var plain = SecureStringUtilities.ToPlainString(secure);

        Assert.True(hasher.VerifyPassword(plain, hash));
    }
}
