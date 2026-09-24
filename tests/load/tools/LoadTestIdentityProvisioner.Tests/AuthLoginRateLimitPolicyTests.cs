using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class AuthLoginRateLimitPolicyTests
{
    [Fact]
    public void RecommendedIntervalIsSaferThanFivePerMinute()
    {
        var minInterval = 60.0 / AuthLoginRateLimitPolicy.LoginPermitLimit;
        Assert.True(AuthLoginRateLimitPolicy.RecommendedLoginIntervalSeconds > minInterval);
    }
}
