namespace MovieApp.Application.Services.Notifications;

public static class NotificationInboxRetention
{
    public static DateTime GetReadExpirationCutoffUtc(DateTime utcNow, int readRetentionDays) =>
        utcNow.AddDays(-readRetentionDays);

    public static bool IsInboxVisible(DateTime? readAtUtc, DateTime readExpirationCutoffUtc) =>
        readAtUtc is null || readAtUtc > readExpirationCutoffUtc;
}
