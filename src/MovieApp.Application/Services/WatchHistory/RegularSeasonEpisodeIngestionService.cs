using System.Diagnostics;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.WatchHistory;

public sealed class RegularSeasonEpisodeIngestionService(
    ITvShowRepository tvShowRepository,
    ISeasonRepository seasonRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ITvShowCatalogSyncStateService catalogSyncStateService) : IRegularSeasonEpisodeIngestionService
{
    public async Task<RegularSeasonEpisodeIngestionResult> IngestMissingSeasonsAsync(
        Guid tvShowId,
        IReadOnlyList<int> seasonNumbers,
        CancellationToken cancellationToken = default)
    {
        if (seasonNumbers.Count == 0)
        {
            return new RegularSeasonEpisodeIngestionResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var identity = await tvShowRepository.GetExternalIdsByIdAsync(tvShowId, cancellationToken);
        if (identity is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        var externalId = externalIdResolver.Resolve(identity.TmdbId, identity.TvdbId, identity.ImdbId);
        if (externalId is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        var fetchMetrics = await FetchProviderSeasonsAsync(
            externalId,
            seasonNumbers,
            cancellationToken);

        var providerDetails = new List<SeasonProviderDetails>(seasonNumbers.Count);
        foreach (var seasonNumber in seasonNumbers.OrderBy(number => number))
        {
            if (!fetchMetrics.DetailsBySeason.TryGetValue(seasonNumber, out var details) || details is null)
            {
                throw new NotFoundException(
                    $"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            providerDetails.Add(details);
        }

        var persistenceStopwatch = Stopwatch.StartNew();
        var persistenceMetrics = await seasonRepository.UpsertSeasonsFromProviderAsync(
            tvShowId,
            providerDetails,
            cancellationToken);
        persistenceStopwatch.Stop();

        await catalogSyncStateService.MarkRefreshedAsync(
            tvShowId,
            TvShowCatalogRefreshReason.DetailHydration,
            DateTime.UtcNow,
            cancellationToken);

        return new RegularSeasonEpisodeIngestionResult(
            SeasonsPersisted: providerDetails.Count,
            ProviderSeasonFetchWallMs: fetchMetrics.WallMs,
            ProviderSeasonFetchAccumulatedMs: fetchMetrics.AccumulatedMs,
            MaxSeasonProviderFetchMs: fetchMetrics.MaxSeasonMs,
            PersistenceMs: persistenceStopwatch.ElapsedMilliseconds,
            SaveChangesCount: 1,
            PersistenceExistingDataLoadMs: persistenceMetrics.ExistingDataLoadMs,
            PersistenceMutationMs: persistenceMetrics.MutationPreparationMs,
            PersistenceSaveChangesMs: persistenceMetrics.SaveChangesMs,
            PersistenceIncomingEpisodeCount: persistenceMetrics.IncomingEpisodeCount,
            PersistenceAddedEpisodeCount: persistenceMetrics.AddedEpisodeCount,
            PersistenceUpdatedEpisodeCount: persistenceMetrics.UpdatedEpisodeCount);
    }

    private async Task<ProviderFetchMetrics> FetchProviderSeasonsAsync(
        string externalTvShowId,
        IReadOnlyList<int> seasonNumbers,
        CancellationToken cancellationToken)
    {
        var wallStopwatch = Stopwatch.StartNew();
        var maxParallel = RegularSeasonEpisodeIngestionConcurrency.MaxParallelProviderFetches(seasonNumbers.Count);
        using var concurrencyGate = new SemaphoreSlim(maxParallel, maxParallel);

        var accumulatedMs = 0L;
        var maxSeasonMs = 0L;
        var detailsBySeason = new Dictionary<int, SeasonProviderDetails?>();

        var fetchTasks = seasonNumbers.Select(async seasonNumber =>
        {
            await concurrencyGate.WaitAsync(cancellationToken);
            try
            {
                var seasonStopwatch = Stopwatch.StartNew();
                var details = await tvShowDataProvider.GetSeasonAsync(
                    externalTvShowId,
                    seasonNumber,
                    cancellationToken);
                seasonStopwatch.Stop();

                var seasonMs = seasonStopwatch.ElapsedMilliseconds;
                lock (detailsBySeason)
                {
                    accumulatedMs += seasonMs;
                    maxSeasonMs = Math.Max(maxSeasonMs, seasonMs);
                    detailsBySeason[seasonNumber] = details;
                }
            }
            finally
            {
                concurrencyGate.Release();
            }
        });

        await Task.WhenAll(fetchTasks);
        wallStopwatch.Stop();

        return new ProviderFetchMetrics(
            detailsBySeason,
            wallStopwatch.ElapsedMilliseconds,
            accumulatedMs,
            maxSeasonMs);
    }

    private sealed record ProviderFetchMetrics(
        Dictionary<int, SeasonProviderDetails?> DetailsBySeason,
        long WallMs,
        long AccumulatedMs,
        long MaxSeasonMs);
}
