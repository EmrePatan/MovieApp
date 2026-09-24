using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.WatchHistory;

/// <summary>
/// A show is completed only when it can no longer receive new regular episodes (Ended/Canceled)
/// and every regular episode has been watched. Returning or unconfirmed shows stay "watching"
/// even when the user is caught up. Regular episode totals fall back to the TMDB season summary
/// count for seasons whose episodes are not ingested yet, so partial ingestion never looks complete.
/// Persistence queries mirror these rules in SQL; keep them in sync.
/// </summary>
public static class TvShowCompletionPolicy
{
    public static bool IsConcluded(TvShowStatus status) =>
        status is TvShowStatus.Ended or TvShowStatus.Canceled;

    public static bool IsCompleted(bool isConcluded, int regularTotalEpisodes, int regularWatchedEpisodes) =>
        isConcluded && IsCaughtUp(regularTotalEpisodes, regularWatchedEpisodes);

    public static bool IsCaughtUp(int regularTotalEpisodes, int regularWatchedEpisodes) =>
        regularTotalEpisodes > 0 && regularWatchedEpisodes >= regularTotalEpisodes;

    public static int ResolveSeasonEpisodeTotal(int ingestedEpisodeCount, int? summaryEpisodeCount) =>
        ingestedEpisodeCount > 0 ? ingestedEpisodeCount : Math.Max(0, summaryEpisodeCount ?? 0);
}
