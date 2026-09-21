using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.TvShowFollows;

internal static partial class TvShowFollowBaselineLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "TV follow baseline completed for show {TvShowId} in {TotalMs}ms (summary={SummaryHydrationMs}ms, providerSeasonFetch={ProviderSeasonFetchMs}ms, seasonPersistence={SeasonPersistenceMs}ms, catalogReload={CatalogReloadMs}ms, markRefreshed={MarkRefreshedMs}ms, releaseScan={ReleaseScanMs}ms, baselineCompletion={BaselineCompletionMs}ms, seasonsHydrated={SeasonsHydrated}, episodesHydrated={EpisodesHydrated}).")]
    internal static partial void LogBaselineCompleted(
        ILogger logger,
        Guid tvShowId,
        long totalMs,
        long summaryHydrationMs,
        long providerSeasonFetchMs,
        long seasonPersistenceMs,
        long catalogReloadMs,
        long markRefreshedMs,
        long releaseScanMs,
        long baselineCompletionMs,
        int seasonsHydrated,
        int episodesHydrated);
}
