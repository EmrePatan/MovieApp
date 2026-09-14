using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class TvShowFollowBaselineService(
    ITvShowFollowRepository tvShowFollowRepository,
    ITvShowRepository tvShowRepository,
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    IReleaseDetectionCatalogRepository releaseDetectionCatalogRepository,
    ISeasonRepository seasonRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    IReleaseDetector releaseDetector) : ITvShowFollowBaselineService
{
    private const int MaxConcurrentSeasonHydrations = 3;

    public async Task EstablishAsync(TvShowFollow follow, CancellationToken cancellationToken = default)
    {
        var currentFollow = await tvShowFollowRepository.GetForUserAndTvShowForUpdateAsync(
            follow.UserId,
            follow.TvShowId,
            cancellationToken);

        if (currentFollow is null)
        {
            throw new NotFoundException("The requested TV show follow was not found.");
        }

        follow = currentFollow;

        if (follow.IsBaselineEstablished)
        {
            return;
        }

        if (!follow.NotifyFromUtc.HasValue)
        {
            throw new InvalidOperationException("NotifyFromUtc must be set before baseline establishment.");
        }

        var boundaryUtc = follow.NotifyFromUtc.Value;
        var boundaryDate = DateOnly.FromDateTime(boundaryUtc);
        var tvShowId = follow.TvShowId;

        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        var providerCatalogRefreshed = false;

        if (TvShowFollowBaselineCatalogRules.NeedsSeasonSummaries(tvShow.Seasons))
        {
            var hydrationResult = await seasonSummaryHydrator.EnsureSeasonSummariesAsync(
                tvShowId,
                cancellationToken);
            if (hydrationResult.ProviderCatalogRefreshed)
            {
                providerCatalogRefreshed = true;
            }
        }

        var seasons = await releaseDetectionCatalogRepository.GetSeasonsWithEpisodesAsync(
            tvShowId,
            cancellationToken);

        var seasonsToHydrate = DetermineSeasonsToHydrate(seasons, boundaryDate);
        if (seasonsToHydrate.Count > 0)
        {
            tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
            if (tvShow is null)
            {
                throw new NotFoundException("The requested TV show was not found.");
            }

            var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
            if (externalId is null)
            {
                throw new TvShowFollowBaselineException(
                    "TV show provider identity is unavailable for required baseline hydration.");
            }

            await HydrateSeasonsAsync(tvShowId, externalId, seasonsToHydrate, cancellationToken);
            providerCatalogRefreshed = true;
        }

        if (providerCatalogRefreshed)
        {
            await catalogSyncStateService.MarkRefreshedAsync(
                tvShowId,
                TvShowCatalogRefreshReason.FollowBaseline,
                DateTime.UtcNow,
                cancellationToken);
        }

        await releaseDetector.ScanTvShowAsync(
            tvShowId,
            ReleaseDetectionMode.BaselineAbsorb,
            boundaryDate,
            cancellationToken);

        follow.EstablishBaseline(DateTime.UtcNow);
        await tvShowFollowRepository.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<int> DetermineSeasonsToHydrate(
        IReadOnlyList<Season> seasons,
        DateOnly boundaryDate)
    {
        var seasonsToHydrate = new List<int>();

        foreach (var season in seasons.Where(season => season.SeasonNumber >= 1))
        {
            var regularEpisodes = season.Episodes
                .Where(episode => episode.EpisodeNumber >= 1)
                .ToList();

            if (TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(
                    season,
                    regularEpisodes.Count,
                    boundaryDate,
                    regularEpisodes))
            {
                seasonsToHydrate.Add(season.SeasonNumber);
            }
        }

        return seasonsToHydrate;
    }

    private async Task HydrateSeasonsAsync(
        Guid tvShowId,
        string externalId,
        IReadOnlyList<int> seasonNumbers,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < seasonNumbers.Count; index += MaxConcurrentSeasonHydrations)
        {
            var batch = seasonNumbers.Skip(index).Take(MaxConcurrentSeasonHydrations).ToArray();
            var hydrationTasks = batch
                .Select(seasonNumber => HydrateSeasonAsync(
                    tvShowId,
                    externalId,
                    seasonNumber,
                    cancellationToken))
                .ToArray();

            await Task.WhenAll(hydrationTasks);
        }
    }

    private async Task HydrateSeasonAsync(
        Guid tvShowId,
        string externalId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        var providerSeason = await tvShowDataProvider.GetSeasonAsync(
            externalId,
            seasonNumber,
            cancellationToken);

        if (providerSeason is null)
        {
            throw new TvShowFollowBaselineException(
                $"Unable to hydrate season {seasonNumber} required for follow baseline.");
        }

        await seasonRepository.UpsertFromProviderAsync(tvShowId, providerSeason, cancellationToken);
    }
}
