namespace MovieApp.Infrastructure.Configuration;

public sealed class ResendVerificationEmailOptions
{
    public const string SectionName = "Authentication:EmailVerification:Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "MovieApp";

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
