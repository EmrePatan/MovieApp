using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Services.TvShowChanges;

public sealed class TvShowChangesTargetedRefreshService(
    ITvShowRepository tvShowRepository,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ISeasonRepository seasonRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    IReleaseDetectionCatalogRepository releaseDetectionCatalogRepository,
    IReleaseDetector releaseDetector,
    ITvShowCatalogSyncStateService catalogSyncStateService) : ITvShowChangesTargetedRefreshService
{
    public async Task RefreshFollowedShowAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
        if (externalId is null)
        {
            throw new InvalidOperationException(
                $"TV show '{tvShowId}' does not have a resolvable provider identity.");
        }

        var providerDetails = await tvShowDataProvider.GetTvShowAsync(externalId, cancellationToken);
        if (providerDetails is null)
        {
            throw new InvalidOperationException(
                $"Provider TV show details were unavailable for TV show '{tvShowId}'.");
        }

        await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            providerDetails,
            enrichKeywords: true,
            cancellationToken);

        var seasons = await releaseDetectionCatalogRepository.GetSeasonsWithEpisodesAsync(
            tvShowId,
            cancellationToken);

        var seasonsToHydrate = TvShowChangesRefreshRules.DetermineSeasonsToHydrate(seasons, boundaryDate);
        foreach (var seasonNumber in seasonsToHydrate)
        {
            var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                externalId,
                seasonNumber,
                cancellationToken);

            if (providerSeason is null)
            {
                throw new InvalidOperationException(
                    $"Provider season {seasonNumber} was unavailable for TV show '{tvShowId}'.");
            }

            await seasonRepository.UpsertFromProviderAsync(tvShowId, providerSeason, cancellationToken);
        }

        await releaseDetector.ScanTvShowAsync(
            tvShowId,
            ReleaseDetectionMode.PostRefresh,
            boundaryDate,
            cancellationToken);

        await catalogSyncStateService.MarkChangesSyncAsync(
            tvShowId,
            DateTime.UtcNow,
            changeSignalDate,
            cancellationToken);
    }
}
