namespace MovieApp.Infrastructure.Configuration;

public sealed class ResendVerificationEmailOptions
{
    public const string SectionName = "Authentication:EmailVerification:Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Movie Cave";

    /// <summary>
    /// Optional absolute HTTPS URL for the cinematic hero image in verification emails.
    /// When empty, a gradient hero renders so delivery is not blocked.
    /// </summary>
    public string HeroImageUrl { get; set; } = string.Empty;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
