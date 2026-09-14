using System.Text.RegularExpressions;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Validation;

public static partial class PushDeviceValidator
{
    [GeneratedRegex(@"^(Expo|Exponent)PushToken\[[A-Za-z0-9_-]+\]$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpoPushTokenRegex();

    public static void ValidateRegistration(string expoPushToken, string platform)
    {
        if (string.IsNullOrWhiteSpace(expoPushToken))
        {
            throw new Exceptions.ValidationException("Expo push token is required.");
        }

        var trimmedToken = expoPushToken.Trim();
        if (trimmedToken.Length > 256 || !ExpoPushTokenRegex().IsMatch(trimmedToken))
        {
            throw new Exceptions.ValidationException("Expo push token format is invalid.");
        }

        if (!TryParsePlatform(platform, out _))
        {
            throw new Exceptions.ValidationException("Platform must be 'ios' or 'android'.");
        }
    }

    public static void ValidateUnregister(string expoPushToken)
    {
        if (string.IsNullOrWhiteSpace(expoPushToken))
        {
            throw new Exceptions.ValidationException("Expo push token is required.");
        }

        var trimmedToken = expoPushToken.Trim();
        if (trimmedToken.Length > 256 || !ExpoPushTokenRegex().IsMatch(trimmedToken))
        {
            throw new Exceptions.ValidationException("Expo push token format is invalid.");
        }
    }

    public static bool TryParsePlatform(string platform, out PushDevicePlatform parsedPlatform)
    {
        parsedPlatform = default;

        if (string.IsNullOrWhiteSpace(platform))
        {
            return false;
        }

        return platform.Trim().ToLowerInvariant() switch
        {
            "ios" => Assign(PushDevicePlatform.Ios, out parsedPlatform),
            "android" => Assign(PushDevicePlatform.Android, out parsedPlatform),
            _ => false
        };
    }

    private static bool Assign(PushDevicePlatform value, out PushDevicePlatform parsedPlatform)
    {
        parsedPlatform = value;
        return true;
    }
}
