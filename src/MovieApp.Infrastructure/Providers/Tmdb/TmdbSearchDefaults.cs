namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbSearchDefaults
{
    /// <summary>
    /// TMDB movie search returns a fixed number of results per page.
    /// The API does not support arbitrary page sizes.
    /// </summary>
    public const int ResultsPerPage = 20;
}
