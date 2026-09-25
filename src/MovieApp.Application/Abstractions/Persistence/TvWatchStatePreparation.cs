namespace MovieApp.Application.Abstractions.Persistence;

public sealed record TvWatchStatePreparation(
    bool TvShowExists,
    bool IngestionRequired,
    IReadOnlyList<int> MissingSeasonNumbers,
    IReadOnlyList<Guid> EpisodeIds,
    int RegularSeasonCount,
    int SeasonsWithEpisodeRowsCount)
{
    public static TvWatchStatePreparation NotFound() =>
        new(
            TvShowExists: false,
            IngestionRequired: false,
            MissingSeasonNumbers: [],
            EpisodeIds: [],
            RegularSeasonCount: 0,
            SeasonsWithEpisodeRowsCount: 0);
}
