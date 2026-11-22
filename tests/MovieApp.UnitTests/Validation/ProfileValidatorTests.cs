using MovieApp.Application.Models.Identity;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Validation;

public sealed class ProfileValidatorTests
{
    [Fact]
    public void ValidateDisplayNameAcceptsValidName()
    {
        var result = ProfileValidator.ValidateDisplayName("John Doe");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateDisplayNameRejectsEmptyValue()
    {
        var result = ProfileValidator.ValidateDisplayName("   ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateDisplayNameRejectsTooLongValue()
    {
        var result = ProfileValidator.ValidateDisplayName(new string('a', ProfileValidator.DisplayNameMaxLength + 1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateEmailAcceptsValidEmail()
    {
        var result = ProfileValidator.ValidateEmail("user@example.com");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEmailRejectsInvalidFormat()
    {
        var result = ProfileValidator.ValidateEmail("not-an-email");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCurrentPasswordRequiresValue()
    {
        var result = ProfileValidator.ValidateCurrentPassword(" ");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateBioRejectsTooLongValue()
    {
        var result = ProfileValidator.ValidateBio(new string('a', ProfileValidator.BioMaxLength + 1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateSocialLinksRejectsInvalidWebsite()
    {
        var result = ProfileValidator.ValidateSocialLinks(
            new ProfileSocialLinksResult(Website: "not a url :::"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateSocialLinksAcceptsHandlesAndWebsite()
    {
        var result = ProfileValidator.ValidateSocialLinks(
            new ProfileSocialLinksResult(Instagram: "@emre", Website: "https://cave.app"));

        Assert.True(result.IsValid);
    }
}
