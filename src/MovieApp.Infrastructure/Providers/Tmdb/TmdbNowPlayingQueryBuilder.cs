namespace MovieApp.Infrastructure.Providers.Tmdb;

public static class TmdbNowPlayingQueryBuilder
{
    public static string BuildQuery(string releaseRegion, int page) =>
        $"region={Uri.EscapeDataString(releaseRegion.Trim().ToUpperInvariant())}&page={page}";
}
