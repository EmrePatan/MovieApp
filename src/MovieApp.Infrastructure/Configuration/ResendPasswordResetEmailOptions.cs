namespace MovieApp.Infrastructure.Configuration;

public sealed class ResendPasswordResetEmailOptions
{
    public const string SectionName = "Authentication:PasswordReset:Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Movie Cave";

    /// <summary>
    /// Optional absolute or app-root-relative HTTPS URL for the cinematic hero image.
    /// Relative paths are resolved against App:PublicBaseUrl.
    /// </summary>
    public string HeroImageUrl { get; set; } = string.Empty;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
