using MovieApp.Application.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class PasswordResetTokenGeneratorTests
{
    [Fact]
    public void GenerateTokenProducesUniqueBase64UrlValues()
    {
        var first = PasswordResetTokenGenerator.GenerateToken();
        var second = PasswordResetTokenGenerator.GenerateToken();

        Assert.NotEqual(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }
}
