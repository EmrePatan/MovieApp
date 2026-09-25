namespace MovieApp.Infrastructure.Configuration;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Authentication:RateLimit";

    public int LoginPermitLimit { get; set; } = 5;

    public int LoginWindowMinutes { get; set; } = 1;

    public int SocialPermitLimit { get; set; } = 10;

    public int SocialWindowMinutes { get; set; } = 1;

    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowMinutes { get; set; } = 10;

    public int ForgotPasswordPermitLimit { get; set; } = 3;

    public int ForgotPasswordWindowMinutes { get; set; } = 15;

    public int ResetPasswordPermitLimit { get; set; } = 5;

    public int ResetPasswordWindowMinutes { get; set; } = 15;

    public int ResendVerificationPermitLimit { get; set; } = 3;

    public int ResendVerificationWindowMinutes { get; set; } = 15;

    public int VerifyEmailPermitLimit { get; set; } = 5;

    public int VerifyEmailWindowMinutes { get; set; } = 15;

    public int RefreshPermitLimit { get; set; } = 30;

    public int RefreshWindowMinutes { get; set; } = 1;
}
