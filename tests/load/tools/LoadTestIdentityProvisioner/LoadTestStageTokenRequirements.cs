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
    public const int LoginThrottleSeconds = AuthLoginRateLimitPolicy.RecommendedLoginIntervalSeconds;

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
        var requiredAtStageStart = CapacityStageDurationMinutes + PreflightAndReportMarginMinutes;
        var buffer = 5;
        var fromLifetime = jwtLifetimeMinutes - mintSpread - requiredAtStageStart - buffer;
        var floor = identityCount <= 50 ? 30 : 25;
        return Math.Max(floor, fromLifetime);
    }

    /// <summary>
    /// Minutes remaining on the first-minted JWT when the last identity is minted (serial login).
    /// </summary>
    public static int OldestTokenRemainingMinutesAfterFullMint(
        int identityCount,
        int jwtLifetimeMinutes = DefaultJwtLifetimeMinutes)
    {
        return jwtLifetimeMinutes - MintSpreadMinutes(identityCount);
    }

    /// <summary>VUs per identity when reusing a fixed pool (e.g. 150 VU / 100 identities = 1.5).</summary>
    public static double IdentityReuseRatio(int stageVus, int identityCount)
    {
        if (identityCount < 1)
        {
            return double.PositiveInfinity;
        }

        return (double)stageVus / identityCount;
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
