using MovieApp.Application.Services.PushNotifications;

namespace MovieApp.UnitTests.PushNotifications;

public sealed class PushNotificationRetryPolicyTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    public void CalculateNextAttemptUtc_UsesDeterministicBackoff(int attemptCount, int expectedMinutes)
    {
        var nextAttempt = PushNotificationRetryPolicy.CalculateNextAttemptUtc(attemptCount, UtcNow, 5);

        Assert.Equal(UtcNow.AddMinutes(expectedMinutes), nextAttempt);
    }

    [Fact]
    public void CalculateNextAttemptUtc_ReturnsNullWhenMaxAttemptsReached()
    {
        Assert.Null(PushNotificationRetryPolicy.CalculateNextAttemptUtc(5, UtcNow, 5));
    }

    [Fact]
    public void HasExceededMaxAttempts_IsTrueAtLimit()
    {
        Assert.True(PushNotificationRetryPolicy.HasExceededMaxAttempts(5, 5));
        Assert.False(PushNotificationRetryPolicy.HasExceededMaxAttempts(4, 5));
    }
}
