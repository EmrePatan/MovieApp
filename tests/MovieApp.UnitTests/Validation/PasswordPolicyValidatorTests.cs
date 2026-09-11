using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class PasswordPolicyValidatorTests
{
    [Fact]
    public void ValidateAcceptsValidPassword()
    {
        var result = PasswordPolicyValidator.Validate("StrongPassword123");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsShortPassword()
    {
        var result = PasswordPolicyValidator.Validate("short");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsWhitespaceOnlyPassword()
    {
        var result = PasswordPolicyValidator.Validate("        ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsTooLongPassword()
    {
        var result = PasswordPolicyValidator.Validate(new string('a', PasswordPolicyValidator.MaxLength + 1));

        Assert.False(result.IsValid);
    }
}
