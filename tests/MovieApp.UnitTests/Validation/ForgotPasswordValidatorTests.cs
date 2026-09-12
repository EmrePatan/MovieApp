using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class ForgotPasswordValidatorTests
{
    [Fact]
    public void ValidateRejectsMissingEmail()
    {
        var result = ForgotPasswordValidator.Validate("   ");

        Assert.False(result.IsValid);
        Assert.Equal("Email is required.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateRejectsTooLongEmail()
    {
        var result = ForgotPasswordValidator.Validate($"{new string('a', 310)}@example.com");

        Assert.False(result.IsValid);
        Assert.Equal("Email must not exceed 320 characters.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateAcceptsValidEmail()
    {
        var result = ForgotPasswordValidator.Validate(" user@example.com ");

        Assert.True(result.IsValid);
    }
}
