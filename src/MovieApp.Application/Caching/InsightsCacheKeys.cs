namespace MovieApp.Application.Caching;

public static class InsightsCacheKeys
{
    public const string Prefix = "insights:";

    public const string Version = "v1";

    public static string Generation(Guid userId) =>
        $"{Prefix}gen:{userId:N}:{Version}";

    public static string Summary(Guid userId, string? timeZoneId, long generation = 0) =>
        $"{Prefix}summary:{userId:N}:{generation}:{NormalizeTimeZone(timeZoneId)}:{Version}";

    public static string Analytics(Guid userId, string? timeZoneId, long generation = 0) =>
        $"{Prefix}analytics:{userId:N}:{generation}:{NormalizeTimeZone(timeZoneId)}:{Version}";

    public static string V3(Guid userId, string? timeZoneId, int year, long generation = 0) =>
        $"{Prefix}v3:{userId:N}:{generation}:{NormalizeTimeZone(timeZoneId)}:{year}:{Version}";

    private static string NormalizeTimeZone(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId)
            ? "none"
            : timeZoneId.Trim().ToLowerInvariant();
}
