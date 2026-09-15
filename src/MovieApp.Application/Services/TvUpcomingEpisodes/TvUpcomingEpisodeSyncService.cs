using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.TvUpcomingEpisodes;

namespace MovieApp.Application.Services.TvUpcomingEpisodes;

public sealed class TvUpcomingEpisodeSyncService(
    ITvUpcomingEpisodeSyncRepository syncRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ISeasonRepository seasonRepository,
    ITvShowCatalogSyncStateRepository catalogSyncStateRepository,
    IOptions<TvUpcomingEpisodeSyncOptions> options) : ITvUpcomingEpisodeSyncService
{
    public async Task<TvUpcomingEpisodeSyncBatchResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return new TvUpcomingEpisodeSyncBatchResult(0, 0, 0, 0);
        }

        var batchSize = options.Value.BatchSize;
        var staleBeforeUtc = DateTime.UtcNow.AddHours(-options.Value.FreshnessTtlHours);
        var candidates = await syncRepository.SelectStaleFollowedShowsAsync(
            batchSize,
            staleBeforeUtc,
            cancellationToken);

        if (candidates.Count == 0)
        {
            return new TvUpcomingEpisodeSyncBatchResult(0, 0, 0, 0);
        }

        var succeeded = 0;
        var failed = 0;
        var hydrated = 0;
        var syncedAtUtc = DateTime.UtcNow;

        foreach (var candidate in candidates)
        {
            try
            {
                var externalId = externalIdResolver.Resolve(
                    candidate.TmdbId,
                    candidate.TvdbId,
                    candidate.ImdbId);

                if (externalId is null)
                {
                    failed++;
                    continue;
                }

                var providerDetails = await tvShowDataProvider.GetTvShowAsync(externalId, cancellationToken);
                if (providerDetails is null)
                {
                    failed++;
                    continue;
                }

                var nextEpisode = providerDetails.NextEpisodeToAir;
                if (nextEpisode?.AirDate is not null)
                {
                    var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                        externalId,
                        nextEpisode.SeasonNumber,
                        cancellationToken);

                    if (providerSeason is null)
                    {
                        failed++;
                        continue;
                    }

                    await seasonRepository.UpsertFromProviderAsync(
                        candidate.TvShowId,
                        providerSeason,
                        cancellationToken);
                    hydrated++;
                }

                await catalogSyncStateRepository.MarkUpcomingEpisodeSyncAsync(
                    candidate.TvShowId,
                    syncedAtUtc,
                    cancellationToken);
                succeeded++;
            }
            catch
            {
                failed++;
            }
        }

        return new TvUpcomingEpisodeSyncBatchResult(candidates.Count, succeeded, failed, hydrated);
    }
}
