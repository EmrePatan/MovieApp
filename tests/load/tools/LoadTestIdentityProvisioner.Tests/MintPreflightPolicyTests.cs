namespace MovieApp.LoadTestIdentityProvisioner.Tests;

/// <summary>
/// Documents Mint-LoadTestTokens.ps1 behavior enforced by script review + operator runbook.
/// </summary>
public sealed class MintPreflightPolicyTests
{
    [Fact]
    public void DefaultThrottleMatchesAuthLoginRateLimitPolicy()
    {
        Assert.Equal(
            MovieApp.LoadTestIdentityProvisioner.AuthLoginRateLimitPolicy.RecommendedLoginIntervalSeconds,
            15);
    }
}
