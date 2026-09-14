namespace MovieApp.Application.Services.PushNotifications;

public static class ExpoPushErrorClassifier
{
    private static readonly HashSet<string> PermanentDeviceErrors =
    [
        "DeviceNotRegistered",
        "InvalidCredentials",
        "InvalidRegistration"
    ];

    private static readonly HashSet<string> PermanentDeliveryErrors =
    [
        "MessageTooBig",
        "MismatchSenderId"
    ];

    private static readonly HashSet<string> RetryableErrors =
    [
        "MessageRateExceeded",
        "ProviderError",
        "ExpoServerError",
        "ServiceUnavailable",
        "Timeout"
    ];

    public static bool IsPermanentDeviceError(string? errorCode) =>
        !string.IsNullOrWhiteSpace(errorCode) && PermanentDeviceErrors.Contains(errorCode);

    public static bool IsPermanentDeliveryError(string? errorCode) =>
        !string.IsNullOrWhiteSpace(errorCode) && PermanentDeliveryErrors.Contains(errorCode);

    public static bool IsRetryableError(string? errorCode) =>
        string.IsNullOrWhiteSpace(errorCode) || RetryableErrors.Contains(errorCode);

    public static (bool IsPermanent, bool IsRetryable, bool DeactivateDevice) ClassifyTicketError(string? errorCode)
    {
        if (IsPermanentDeviceError(errorCode))
        {
            return (true, false, true);
        }

        if (IsPermanentDeliveryError(errorCode))
        {
            return (true, false, false);
        }

        if (IsRetryableError(errorCode))
        {
            return (false, true, false);
        }

        return (true, false, false);
    }

    public static (bool IsPermanent, bool IsRetryable, bool DeactivateDevice) ClassifyReceiptError(string? errorCode) =>
        ClassifyTicketError(errorCode);
}
