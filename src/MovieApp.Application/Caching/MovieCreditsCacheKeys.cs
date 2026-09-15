namespace MovieApp.Application.Caching;

public static class MovieCreditsCacheKeys
{
    public const string Prefix = "movie-credits:";

    public const string Version = "v2";

    public static string Create(Guid movieId) => $"{Prefix}{movieId}:{Version}";
}
