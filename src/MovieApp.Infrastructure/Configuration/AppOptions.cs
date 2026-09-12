namespace MovieApp.Infrastructure.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Public HTTPS base URL for the API (for example https://api.example.com).
    /// Used for externally reachable API references; password-reset deep links use
    /// <see cref="MovieApp.Application.Configuration.PasswordResetOptions.BaseUrl"/> separately.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
