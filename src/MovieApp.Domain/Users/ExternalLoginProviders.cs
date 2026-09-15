namespace MovieApp.Domain.Users;

public static class ExternalLoginProviders
{
    public const string Google = "google";

    public const string Apple = "apple";

    public static bool IsSupported(string? provider) =>
        string.Equals(provider, Google, StringComparison.Ordinal) ||
        string.Equals(provider, Apple, StringComparison.Ordinal);

    public static string Normalize(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var normalized = provider.Trim().ToLowerInvariant();
        if (!IsSupported(normalized))
        {
            throw new ArgumentException($"Unsupported social provider '{provider}'.", nameof(provider));
        }

        return normalized;
    }
}
