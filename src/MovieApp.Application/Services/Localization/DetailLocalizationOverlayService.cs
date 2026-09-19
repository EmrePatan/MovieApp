using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.Localization;

public sealed class DetailLocalizationOverlayService(
    ILocalizedDetailDataProvider localizedDetailDataProvider,
    ICacheService cacheService) : IDetailLocalizationOverlayService
{
    private static readonly TimeSpan CatalogOverlayCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CollectionOverlayCacheTtl = TimeSpan.FromHours(24);

    public async Task<MovieDetailsResult> ApplyMovieOverlayAsync(
        MovieDetailsResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApplyOverlay(canonical.TmdbId, contentLocale))
        {
            return canonical;
        }

        var overlay = await GetOrLoadOverlayAsync(
            DetailLocalizationCacheKeys.Movie(canonical.TmdbId!.Value, contentLocale),
            () => localizedDetailDataProvider.GetMovieLocalizationAsync(
                canonical.TmdbId!.Value,
                contentLocale,
                cancellationToken),
            CatalogOverlayCacheTtl,
            cancellationToken);

        if (overlay is null)
        {
            return canonical;
        }

        var collection = canonical.Collection;
        if (collection is not null)
        {
            collection = collection with
            {
                Name = LocalizationFieldFallback.Choose(collection.Name, overlay.CollectionName)
            };
        }

        return canonical with
        {
            Title = LocalizationFieldFallback.Choose(canonical.Title, overlay.Title),
            Overview = LocalizationFieldFallback.ChooseNullable(canonical.Overview, overlay.Overview),
            Collection = collection
        };
    }

    public async Task<TvShowDetailsResult> ApplyTvShowOverlayAsync(
        TvShowDetailsResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApplyOverlay(canonical.TmdbId, contentLocale))
        {
            return canonical;
        }

        var overlay = await GetOrLoadOverlayAsync(
            DetailLocalizationCacheKeys.TvShow(canonical.TmdbId!.Value, contentLocale),
            () => localizedDetailDataProvider.GetTvShowLocalizationAsync(
                canonical.TmdbId!.Value,
                contentLocale,
                cancellationToken),
            CatalogOverlayCacheTtl,
            cancellationToken);

        if (overlay is null)
        {
            return canonical;
        }

        var seasons = canonical.Seasons
            .Select(season =>
            {
                if (!overlay.SeasonNames.TryGetValue(season.SeasonNumber, out var localizedName))
                {
                    return season;
                }

                return season with
                {
                    Name = LocalizationFieldFallback.ChooseNullable(season.Name, localizedName)
                };
            })
            .ToList();

        return canonical with
        {
            Title = LocalizationFieldFallback.Choose(canonical.Title, overlay.Title),
            Overview = LocalizationFieldFallback.ChooseNullable(canonical.Overview, overlay.Overview),
            Status = LocalizationFieldFallback.Choose(canonical.Status, overlay.Status),
            Seasons = seasons
        };
    }

    public async Task<SeasonResult> ApplySeasonOverlayAsync(
        SeasonResult canonical,
        int tvShowTmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApplyOverlay(tvShowTmdbId, contentLocale))
        {
            return canonical;
        }

        var overlay = await GetTvSeasonOverlayAsync(
            tvShowTmdbId,
            canonical.SeasonNumber,
            contentLocale,
            cancellationToken);

        if (overlay is null)
        {
            return canonical;
        }

        var episodeLocalizations = overlay.Episodes
            .GroupBy(episode => episode.EpisodeNumber)
            .ToDictionary(group => group.Key, group => group.Last());

        var episodes = canonical.Episodes
            .Select(episode =>
            {
                if (!episodeLocalizations.TryGetValue(episode.EpisodeNumber, out var localizedEpisode))
                {
                    return episode;
                }

                return episode with
                {
                    Name = LocalizationFieldFallback.ChooseNullable(episode.Name, localizedEpisode.Name)
                };
            })
            .ToList();

        return canonical with
        {
            Name = LocalizationFieldFallback.ChooseNullable(canonical.Name, overlay.Name),
            Overview = LocalizationFieldFallback.ChooseNullable(canonical.Overview, overlay.Overview),
            Episodes = episodes
        };
    }

    public async Task<EpisodeResult> ApplyEpisodeOverlayAsync(
        EpisodeResult canonical,
        int tvShowTmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldApplyOverlay(tvShowTmdbId, contentLocale))
        {
            return canonical;
        }

        var overlay = await GetTvSeasonOverlayAsync(
            tvShowTmdbId,
            canonical.SeasonNumber,
            contentLocale,
            cancellationToken);

        if (overlay is null)
        {
            return canonical;
        }

        var localizedEpisode = overlay.Episodes
            .LastOrDefault(episode => episode.EpisodeNumber == canonical.EpisodeNumber);

        if (localizedEpisode is null)
        {
            return canonical;
        }

        return canonical with
        {
            Name = LocalizationFieldFallback.ChooseNullable(canonical.Name, localizedEpisode.Name),
            Overview = LocalizationFieldFallback.ChooseNullable(
                canonical.Overview,
                localizedEpisode.Overview)
        };
    }

    public async Task<PersonDetailResult> ApplyPersonOverlayAsync(
        PersonDetailResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return canonical;
        }

        var overlay = await GetOrLoadOverlayAsync(
            DetailLocalizationCacheKeys.Person(canonical.TmdbId, contentLocale),
            () => localizedDetailDataProvider.GetPersonLocalizationAsync(
                canonical.TmdbId,
                contentLocale,
                cancellationToken),
            CatalogOverlayCacheTtl,
            cancellationToken);

        if (overlay is null)
        {
            return canonical;
        }

        var filmographyLookup = overlay.Filmography
            .GroupBy(item => (item.MediaType, item.TmdbId))
            .ToDictionary(group => group.Key, group => group.Last());

        var filmography = canonical.Filmography
            .Select(entry =>
            {
                if (!filmographyLookup.TryGetValue((entry.MediaType, entry.TmdbId), out var localizedEntry))
                {
                    return entry;
                }

                return entry with
                {
                    Title = LocalizationFieldFallback.Choose(entry.Title, localizedEntry.Title),
                    Character = LocalizationFieldFallback.ChooseNullable(entry.Character, localizedEntry.Character)
                };
            })
            .ToList();

        return canonical with
        {
            Biography = LocalizationFieldFallback.ChooseNullable(canonical.Biography, overlay.Biography),
            Filmography = filmography
        };
    }

    public async Task<CollectionDetailResult> ApplyCollectionOverlayAsync(
        CollectionDetailResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return canonical;
        }

        var overlay = await GetOrLoadOverlayAsync(
            DetailLocalizationCacheKeys.Collection(canonical.TmdbId, contentLocale),
            () => localizedDetailDataProvider.GetCollectionLocalizationAsync(
                canonical.TmdbId,
                contentLocale,
                cancellationToken),
            CollectionOverlayCacheTtl,
            cancellationToken);

        if (overlay is null)
        {
            return canonical;
        }

        var parts = canonical.Parts
            .Select(part =>
            {
                if (!overlay.Parts.TryGetValue(part.TmdbId, out var localizedPart))
                {
                    return part;
                }

                return part with
                {
                    Title = LocalizationFieldFallback.Choose(part.Title, localizedPart.Title),
                    Overview = LocalizationFieldFallback.ChooseNullable(part.Overview, localizedPart.Overview)
                };
            })
            .ToList();

        return canonical with
        {
            Name = LocalizationFieldFallback.Choose(canonical.Name, overlay.Name),
            Overview = LocalizationFieldFallback.ChooseNullable(canonical.Overview, overlay.Overview),
            Parts = parts
        };
    }

    private static bool ShouldApplyOverlay(int? tmdbId, string contentLocale) =>
        ContentLocaleResolver.RequiresLocalization(contentLocale) && tmdbId is > 0;

    private Task<TvSeasonDetailLocalizationData?> GetTvSeasonOverlayAsync(
        int tvShowTmdbId,
        int seasonNumber,
        string contentLocale,
        CancellationToken cancellationToken) =>
        GetOrLoadOverlayAsync(
            DetailLocalizationCacheKeys.TvSeason(tvShowTmdbId, seasonNumber, contentLocale),
            () => localizedDetailDataProvider.GetTvSeasonLocalizationAsync(
                tvShowTmdbId,
                seasonNumber,
                contentLocale,
                cancellationToken),
            CatalogOverlayCacheTtl,
            cancellationToken);

    private async Task<T?> GetOrLoadOverlayAsync<T>(
        string cacheKey,
        Func<Task<T?>> fetchOverlay,
        TimeSpan ttl,
        CancellationToken cancellationToken)
        where T : class
    {
        var cached = await cacheService.GetAsync<DetailLocalizationCacheEntry<T>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Data;
        }

        T? overlay;
        try
        {
            overlay = await fetchOverlay();
        }
        catch
        {
            return null;
        }

        if (overlay is null)
        {
            return null;
        }

        await cacheService.SetAsync(
            cacheKey,
            new DetailLocalizationCacheEntry<T> { Data = overlay },
            ttl,
            cancellationToken);

        return overlay;
    }
}
