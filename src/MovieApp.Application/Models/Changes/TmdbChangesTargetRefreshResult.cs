namespace MovieApp.Application.Models.Changes;

public sealed record TmdbChangesTargetRefreshResult(
    TmdbChangesTargetRefreshOutcome Outcome,
    IReadOnlyList<TmdbChangesHydratedSeasonCacheTarget> HydratedSeasons)
{
    public static TmdbChangesTargetRefreshResult Refreshed(
        IReadOnlyList<TmdbChangesHydratedSeasonCacheTarget> hydratedSeasons) =>
        new(TmdbChangesTargetRefreshOutcome.Refreshed, hydratedSeasons);

    public static TmdbChangesTargetRefreshResult SkippedUnavailable() =>
        new(TmdbChangesTargetRefreshOutcome.SkippedUnavailable, []);

    public static TmdbChangesTargetRefreshResult SkippedNotFound() =>
        new(TmdbChangesTargetRefreshOutcome.SkippedNotFound, []);

    public static TmdbChangesTargetRefreshResult Failed() =>
        new(TmdbChangesTargetRefreshOutcome.Failed, []);
}

public sealed record TmdbChangesHydratedSeasonCacheTarget(
    int SeasonNumber,
    IReadOnlyList<int> EpisodeNumbers);
