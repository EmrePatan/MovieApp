namespace MovieApp.Application.Services.WatchHistory;

public interface IRegularSeasonEpisodeIngestionService
{
    Task<RegularSeasonEpisodeIngestionResult> IngestMissingSeasonsAsync(
        Guid tvShowId,
        IReadOnlyList<int> seasonNumbers,
        CancellationToken cancellationToken = default);
}

public sealed record RegularSeasonEpisodeIngestionResult(
    int SeasonsPersisted,
    long ProviderSeasonFetchWallMs,
    long ProviderSeasonFetchAccumulatedMs,
    long MaxSeasonProviderFetchMs,
    long PersistenceMs,
    int SaveChangesCount,
    long PersistenceExistingDataLoadMs,
    long PersistenceMutationMs,
    long PersistenceSaveChangesMs,
    int PersistenceIncomingEpisodeCount,
    int PersistenceAddedEpisodeCount,
    int PersistenceUpdatedEpisodeCount);
