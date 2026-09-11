using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class RegisterUserValidatorTests
{
    [Fact]
    public void ValidateAcceptsValidRegistrationRequest()
    {
        var result = RegisterUserValidator.Validate(new RegisterUserRequest(
            "user@example.com",
            "StrongPassword123",
            "Display Name"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsShortPassword()
    {
        var result = RegisterUserValidator.Validate(new RegisterUserRequest(
            "user@example.com",
            "short",
            "Display Name"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsInvalidEmail()
    {
        var result = RegisterUserValidator.Validate(new RegisterUserRequest(
            "not-an-email",
            "StrongPassword123",
            "Display Name"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateRejectsMissingDisplayName()
    {
        var result = RegisterUserValidator.Validate(new RegisterUserRequest(
            "user@example.com",
            "StrongPassword123",
            " "));

        Assert.False(result.IsValid);
    }
}
