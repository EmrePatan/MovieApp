namespace MovieApp.Application.Configuration;

public sealed class PushNotificationsOptions
{
    public const string SectionName = "PushNotifications";

    public bool Enabled { get; set; } = true;

    public int MaxAttempts { get; set; } = 5;

    public int DispatchBatchSize { get; set; } = 100;

    public int ClaimLeaseMinutes { get; set; } = 5;
}
