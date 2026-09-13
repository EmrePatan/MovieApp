namespace MovieApp.Application.Caching;

public static class ProfileStatisticsCacheKeys
{
    public const string Prefix = "profile-statistics:";

    public const string Version = "v1";

    public static string Generation(Guid userId) =>
        $"{Prefix}generation:{userId:N}:{Version}";

    public static string Create(Guid userId, string? timeZoneId, long generation = 0) =>
        $"{Prefix}{userId:N}:{generation}:{NormalizeTimeZone(timeZoneId)}:{Version}";

    private static string NormalizeTimeZone(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId)
            ? "none"
            : timeZoneId.Trim().ToLowerInvariant();
}
