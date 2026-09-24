namespace MovieApp.LoadTestIdentityProvisioner;

/// <summary>
/// JWT preflight windows for load-test stages (60-minute access tokens, no refresh).
/// </summary>
public static class LoadTestStageTokenRequirements
{
    /// <summary>capacity preset: 3m ramp + 12m hold + 2m ramp-down.</summary>
    public const int CapacityStageDurationMinutes = 17;

    public const int PreflightAndReportMarginMinutes = 5;

    /// <summary>Conservative delay between serial logins (5/min/IP production limit).</summary>
    public const int LoginThrottleSeconds = 13;

    public const int DefaultJwtLifetimeMinutes = 60;

    public const int DefaultLoad60PoolSize = 50;

    public static int MintSpreadMinutes(int identityCount)
    {
        if (identityCount < 1)
        {
            return 0;
        }

        return (int)Math.Ceiling((identityCount - 1) * LoginThrottleSeconds / 60.0);
    }

    /// <summary>
    /// Minimum minutes remaining on the oldest JWT at k6 stage start.
    /// Oldest token is minted first; stage runs after full mint completes.
    /// Requires: jwtLifetime - mintSpread &gt;= stage + margin (plus buffer).
    /// </summary>
    public static int RecommendedMinMinutesUntilStageStart(
        int identityCount = DefaultLoad60PoolSize,
        int jwtLifetimeMinutes = DefaultJwtLifetimeMinutes)
    {
        var mintSpread = MintSpreadMinutes(identityCount);
        var requiredSpan = CapacityStageDurationMinutes + PreflightAndReportMarginMinutes + mintSpread;
        var buffer = 3;
        var fromLifetime = jwtLifetimeMinutes - requiredSpan - buffer;
        var floor = 30;
        return Math.Max(floor, fromLifetime > 0 ? Math.Min(fromLifetime, jwtLifetimeMinutes) : floor);
    }

    public static string ExplainRecommendation(int identityCount = DefaultLoad60PoolSize)
    {
        var minMinutes = RecommendedMinMinutesUntilStageStart(identityCount);
        var mintSpread = MintSpreadMinutes(identityCount);
        return
            $"Capacity stage ~{CapacityStageDurationMinutes}m + preflight/report ~{PreflightAndReportMarginMinutes}m; " +
            $"serial mint spread ~{mintSpread}m ({identityCount} identities @ {LoginThrottleSeconds}s); " +
            $"use Test-LoadTokens -MinMinutesUntilExpiry {minMinutes} at stage start (60m JWT, no refresh).";
    }
}
