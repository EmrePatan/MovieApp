namespace MovieApp.Infrastructure.Configuration;

public sealed class AccountRateLimitOptions
{
    public const string SectionName = "Authentication:AccountRateLimit";

    public int PasswordChangePermitLimit { get; set; } = 5;

    public int PasswordChangeWindowMinutes { get; set; } = 15;

    public int EmailChangePermitLimit { get; set; } = 5;

    public int EmailChangeWindowMinutes { get; set; } = 15;

    public int AccountDeletionPermitLimit { get; set; } = 3;

    public int AccountDeletionWindowMinutes { get; set; } = 60;
}
