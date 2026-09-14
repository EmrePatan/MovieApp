using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.TvShows;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public sealed class GetEpisodeService(
    ITvShowRepository tvShowRepository,
    ISeasonRepository seasonRepository,
    IEpisodeRepository episodeRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ICacheService cacheService) : IGetEpisodeService
{
    private static readonly TimeSpan EpisodeCacheTtl = TimeSpan.FromMinutes(15);

    public async Task<EpisodeResult> GetEpisodeAsync(
        Guid tvShowId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default)
    {
        if (seasonNumber < 1)
        {
            throw new ValidationException("Season number must be at least 1.");
        }

        if (episodeNumber < 1)
        {
            throw new ValidationException("Episode number must be at least 1.");
        }

        var cacheKey = TvShowEpisodeCacheKeys.Create(tvShowId, seasonNumber, episodeNumber);
        var cachedEntry = await cacheService.GetAsync<TvShowEpisodeCacheEntry>(cacheKey, cancellationToken);
        if (cachedEntry is not null)
        {
            return cachedEntry.Result;
        }

        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException($"TV show with id '{tvShowId}' was not found.");
        }

        var season = await seasonRepository.GetByTvShowIdAndSeasonNumberAsync(tvShowId, seasonNumber, cancellationToken);
        var providerCatalogRefreshed = false;
        if (season is null)
        {
            var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
            if (externalId is null)
            {
                throw new NotFoundException(
                    $"Episode {episodeNumber} in season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                externalId,
                seasonNumber,
                cancellationToken);

            if (providerSeason is null)
            {
                throw new NotFoundException(
                    $"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            season = await seasonRepository.UpsertFromProviderAsync(tvShowId, providerSeason, cancellationToken);
            providerCatalogRefreshed = true;
        }

        var episode = await episodeRepository.GetBySeasonIdAndEpisodeNumberAsync(
            season.Id,
            episodeNumber,
            cancellationToken);

        if (episode is null)
        {
            var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
            if (externalId is null)
            {
                throw new NotFoundException(
                    $"Episode {episodeNumber} in season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                externalId,
                seasonNumber,
                cancellationToken);

            if (providerSeason is null)
            {
                throw new NotFoundException(
                    $"Episode {episodeNumber} in season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }

            season = await seasonRepository.UpsertFromProviderAsync(tvShowId, providerSeason, cancellationToken);
            providerCatalogRefreshed = true;
            episode = await episodeRepository.GetBySeasonIdAndEpisodeNumberAsync(
                season.Id,
                episodeNumber,
                cancellationToken);

            if (episode is null)
            {
                throw new NotFoundException(
                    $"Episode {episodeNumber} in season {seasonNumber} for TV show '{tvShowId}' was not found.");
            }
        }

        if (providerCatalogRefreshed)
        {
            await catalogSyncStateService.MarkRefreshedAsync(
                tvShowId,
                TvShowCatalogRefreshReason.DetailHydration,
                DateTime.UtcNow,
                cancellationToken);
        }

        var result = TvShowMapper.ToEpisodeResult(episode, tvShowId, seasonNumber);

        await cacheService.SetAsync(
            cacheKey,
            new TvShowEpisodeCacheEntry { Result = result },
            EpisodeCacheTtl,
            cancellationToken);

        return result;
    }
}
