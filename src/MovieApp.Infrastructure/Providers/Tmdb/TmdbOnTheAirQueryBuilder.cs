namespace MovieApp.Infrastructure.Providers.Tmdb;

public static class TmdbOnTheAirQueryBuilder
{
    public static string BuildQuery(int page) => $"page={page}";
}
