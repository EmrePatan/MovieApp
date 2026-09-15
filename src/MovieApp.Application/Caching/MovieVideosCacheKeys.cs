namespace MovieApp.Application.Caching;

public static class MovieVideosCacheKeys
{
    public const string Prefix = "movie-videos:";

    public const string Version = "v1";

    public static string Create(Guid movieId) => $"{Prefix}{movieId}:{Version}";
}
