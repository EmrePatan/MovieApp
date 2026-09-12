namespace MovieApp.Infrastructure.Configuration;

internal static class JwtSigningKeyProductionRules
{
    internal const int MinimumKeyLength = 32;

    private static readonly string[] DisallowedExactKeys =
    [
        "integration-test-signing-key-must-be-at-least-32-bytes",
        "unit-test-signing-key-must-be-at-least-32-bytes",
        "your_local_development_signing_key_at_least_32_chars",
        "your-local-development-signing-key-at-least-32-chars"
    ];

    internal static bool IsAcceptableProductionSigningKey(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        if (signingKey.Length < MinimumKeyLength)
        {
            return false;
        }

        foreach (var disallowedKey in DisallowedExactKeys)
        {
            if (string.Equals(signingKey, disallowedKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
