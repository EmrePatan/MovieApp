namespace MovieApp.Application.Abstractions.Persistence;

public sealed record SeasonBatchUpsertPersistenceMetrics(
    long TotalMs,
    long AdvisoryLockMs,
    long ExistingDataLoadMs,
    long MutationPreparationMs,
    long SaveChangesMs,
    int SeasonCount,
    int IncomingEpisodeCount,
    int AddedEpisodeCount,
    int UpdatedEpisodeCount,
    int AddedSeasonCount,
    int UpdatedSeasonCount)
{
    public static SeasonBatchUpsertPersistenceMetrics Empty =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
