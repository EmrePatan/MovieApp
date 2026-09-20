namespace MovieApp.Infrastructure.Configuration;

internal static class ResendProductionFromAddressRules
{
    internal static bool IsOnboardingSender(string fromAddress)
    {
        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            return false;
        }

        var normalized = fromAddress.Trim();
        var atIndex = normalized.LastIndexOf('@');
        if (atIndex < 0 || atIndex == normalized.Length - 1)
        {
            return false;
        }

        return normalized[(atIndex + 1)..]
            .Equals("resend.dev", StringComparison.OrdinalIgnoreCase);
    }
}
