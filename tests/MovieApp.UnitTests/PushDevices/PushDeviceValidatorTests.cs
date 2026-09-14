using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.PushDevices;

public sealed class PushDeviceValidatorTests
{
    private const string ValidToken = "ExponentPushToken[abcdefghijklmnopqrstuvwxyz123456]";

    [Fact]
    public void ValidateRegistration_AcceptsValidTokenAndPlatform()
    {
        var exception = Record.Exception(() =>
            PushDeviceValidator.ValidateRegistration(ValidToken, "ios"));

        Assert.Null(exception);
        Assert.True(PushDeviceValidator.TryParsePlatform("android", out var platform));
        Assert.Equal(PushDevicePlatform.Android, platform);
    }

    [Fact]
    public void ValidateRegistration_RejectsInvalidToken()
    {
        Assert.Throws<Application.Exceptions.ValidationException>(() =>
            PushDeviceValidator.ValidateRegistration("not-a-token", "ios"));
    }

    [Fact]
    public void ValidateRegistration_RejectsInvalidPlatform()
    {
        Assert.Throws<Application.Exceptions.ValidationException>(() =>
            PushDeviceValidator.ValidateRegistration(ValidToken, "windows"));
    }
}
