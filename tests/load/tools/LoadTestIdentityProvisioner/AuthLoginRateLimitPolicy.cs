namespace MovieApp.LoadTestIdentityProvisioner;

/// <summary>
/// Mirrors production defaults in appsettings.json Authentication:RateLimit (client IP + /api/auth/login path).
/// Failed attempts consume permits — not only successes.
/// </summary>
public static class AuthLoginRateLimitPolicy
{
    public const int LoginPermitLimit = 5;

    public const int LoginWindowMinutes = 1;

    /// <summary>Conservative spacing: 5 permits / 60s → 12s minimum; use 15s safety margin.</summary>
    public const int RecommendedLoginIntervalSeconds = 15;

    public static int CooldownAfterExhaustionSeconds => LoginWindowMinutes * 60;
}
