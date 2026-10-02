namespace MovieApp.Infrastructure.Providers.Tmdb;

public static class TmdbTrendingWeekQueryBuilder
{
    public static string BuildQuery(int page) => $"page={Math.Max(1, page)}";
}
