namespace MovieApp.Domain.Watchlists;

public static class WatchlistNameNormalizer
{
    public const int MaxLength = 100;

    public static string Normalize(string name) =>
        name.Trim().ToLowerInvariant();
}
