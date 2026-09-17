namespace MovieApp.Application.Configuration;

public sealed class NotificationRetentionOptions
{
    public const string SectionName = "NotificationRetention";

    public int ReadRetentionDays { get; set; } = 7;
}
