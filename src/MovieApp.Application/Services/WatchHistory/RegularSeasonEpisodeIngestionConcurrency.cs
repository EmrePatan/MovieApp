namespace MovieApp.Application.Services.WatchHistory;

internal static class RegularSeasonEpisodeIngestionConcurrency
{
    /// <summary>
    /// Caps parallel TMDB season/detail requests per TV-show ingest. HttpClient is thread-safe;
    /// persistence remains sequential/batched on the scoped DbContext.
    /// </summary>
    internal static int MaxParallelProviderFetches(int seasonCount) =>
        seasonCount <= 0 ? 1 : Math.Min(seasonCount, 6);
}
