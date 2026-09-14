namespace MovieApp.Application.Services.PushNotifications;

public static class PushNotificationRetryPolicy
{
    public static TimeSpan? GetDelayForAttempt(int attemptCount)
    {
        return attemptCount switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromHours(1),
            _ => null
        };
    }

    public static DateTime? CalculateNextAttemptUtc(int attemptCount, DateTime utcNow, int maxAttempts)
    {
        if (attemptCount >= maxAttempts)
        {
            return null;
        }

        var delay = GetDelayForAttempt(attemptCount);
        return delay is null ? null : utcNow + delay.Value;
    }

    public static bool HasExceededMaxAttempts(int attemptCount, int maxAttempts) =>
        attemptCount >= maxAttempts;
}
