namespace MovieApp.Application.Caching;

public static class PersonDetailsCacheKeys
{
    public const string Prefix = "person-details:";

    public static string Create(int tmdbPersonId) => $"{Prefix}{tmdbPersonId}";
}
