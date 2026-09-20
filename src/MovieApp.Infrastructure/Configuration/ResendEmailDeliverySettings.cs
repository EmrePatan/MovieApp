namespace MovieApp.Infrastructure.Configuration;

public sealed record ResendEmailDeliverySettings(
    string ApiKey,
    string FromAddress,
    string FromName)
{
    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}

internal static class ResendEmailDeliverySettingsResolver
{
    internal static ResendEmailDeliverySettings Resolve(
        string apiKey,
        string fromAddress,
        string fromName,
        SharedResendEmailOptions sharedDefaults) =>
        new(
            Coalesce(apiKey, sharedDefaults.ApiKey),
            Coalesce(fromAddress, sharedDefaults.FromAddress),
            Coalesce(fromName, sharedDefaults.FromName, "Movie Cave"));

    private static string Coalesce(string primary, string fallback, string? secondFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        return secondFallback?.Trim() ?? string.Empty;
    }
}
