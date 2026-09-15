namespace MovieApp.Application.Caching;

public static class PersonImagesCacheKeys
{
    public const string Prefix = "person-images:";

    public const string Version = "v1";

    public static string Create(int tmdbPersonId) => $"{Prefix}{tmdbPersonId}:{Version}";
}
