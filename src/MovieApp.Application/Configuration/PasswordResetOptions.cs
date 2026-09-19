namespace MovieApp.Application.Configuration;

public sealed class PasswordResetOptions
{
    public const string SectionName = "Authentication:PasswordReset";

    public int TokenLifetimeMinutes { get; set; } = 60;

    public int DeliveryTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Email delivery provider. Development uses <c>Development</c>.
    /// Production requires <c>Resend</c> with configured Resend settings.
    /// </summary>
    public string EmailProvider { get; set; } = "Development";

    /// <summary>
    /// Base URL for password reset links, without query string.
    /// Example: movieapp://reset-password or https://app.example.com/reset-password
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
