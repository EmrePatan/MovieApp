namespace MovieApp.Application.Caching;

public static class MovieDetailsCacheKeys
{
    public const string Prefix = "movie-details:";

    public static string Create(Guid movieId) => $"{Prefix}{movieId}";

    public static string CollectionProbe(Guid movieId) => $"{Prefix}collection-probe:{movieId}";
}
