using System.Globalization;

namespace MovieApp.Application.Services.ReleaseNotifications;

internal static class ReleaseNotificationAggregation
{
    public static string BuildWindowKey(DateTime releaseAtUtc) =>
        DateOnly.FromDateTime(releaseAtUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
