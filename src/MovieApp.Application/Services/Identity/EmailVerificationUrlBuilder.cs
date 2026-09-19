namespace MovieApp.Application.Services.Identity;

internal static class EmailVerificationUrlBuilder
{
    public static string BuildVerificationUrl(string baseUrl, string rawToken)
    {
        var trimmedBase = baseUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmedBase))
        {
            return $"?token={Uri.EscapeDataString(rawToken)}";
        }

        var separator = trimmedBase.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{trimmedBase}{separator}token={Uri.EscapeDataString(rawToken)}";
    }
}
