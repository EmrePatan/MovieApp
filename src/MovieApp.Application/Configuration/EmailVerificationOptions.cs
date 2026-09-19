namespace MovieApp.Application.Configuration;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "Authentication:EmailVerification";

    public int TokenLifetimeMinutes { get; set; } = 1440;

    public int SmtpDeliveryTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Base URL for email verification links, without query string.
    /// Example: movieapp://verify-email
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
