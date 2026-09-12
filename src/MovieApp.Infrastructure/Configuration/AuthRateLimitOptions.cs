namespace MovieApp.Infrastructure.Configuration;

public sealed class AuthRateLimitOptions
{
    public const string SectionName = "Authentication:RateLimit";

    public int LoginPermitLimit { get; set; } = 5;

    public int LoginWindowMinutes { get; set; } = 1;

    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowMinutes { get; set; } = 10;

    public int ForgotPasswordPermitLimit { get; set; } = 3;

    public int ForgotPasswordWindowMinutes { get; set; } = 15;

    public int ResetPasswordPermitLimit { get; set; } = 5;

    public int ResetPasswordWindowMinutes { get; set; } = 15;
}
