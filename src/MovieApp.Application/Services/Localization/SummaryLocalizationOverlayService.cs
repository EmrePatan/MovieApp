using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Localization;

public sealed class SummaryLocalizationOverlayService(
    ICacheService cacheService,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository) : ISummaryLocalizationOverlayService
{
    public async Task<PaginatedResult<SearchItem>> ApplyToSearchItemsAsync(
        PaginatedResult<SearchItem> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Items.Count == 0)
        {
            return canonical;
        }

        var localizedItems = await Task.WhenAll(
            canonical.Items.Select(item => ApplyToSearchItemAsync(item, contentLocale, cancellationToken)));

        return canonical with { Items = localizedItems };
    }

    public async Task<IReadOnlyList<SearchSuggestion>> ApplyToSearchSuggestionsAsync(
        IReadOnlyList<SearchSuggestion> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Count == 0)
        {
            return canonical;
        }

        var localizedSuggestions = await Task.WhenAll(
            canonical.Select(suggestion => LocalizeSuggestionAsync(suggestion, contentLocale, cancellationToken)));

        return localizedSuggestions;
    }

    public async Task<PaginatedResult<RecommendationItem>> ApplyToRecommendationItemsAsync(
        PaginatedResult<RecommendationItem> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Items.Count == 0)
        {
            return canonical;
        }

        var tmdbIdsByContentId = await ResolveTmdbIdsByContentIdsAsync(canonical.Items, cancellationToken);
        var localizedItems = await Task.WhenAll(
            canonical.Items.Select(item => LocalizeRecommendationItemAsync(
                item,
                tmdbIdsByContentId,
                contentLocale,
                cancellationToken)));

        return canonical with { Items = localizedItems };
    }

    public async Task<HomeResult> ApplyToHomeResultAsync(
        HomeResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Sections.Count == 0)
        {
            return canonical;
        }

        var localizedSections = new List<HomeSection>(canonical.Sections.Count);
        foreach (var section in canonical.Sections)
        {
            var localizedItems = await ApplyToHomeItemsAsync(section.Items, contentLocale, cancellationToken);
            localizedSections.Add(section with { Items = localizedItems });
        }

        return canonical with { Sections = localizedSections };
    }

    public async Task<IReadOnlyList<RecommendationSection>> ApplyToRecommendationSectionsAsync(
        IReadOnlyList<RecommendationSection> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Count == 0)
        {
            return canonical;
        }

        var localizedSections = new List<RecommendationSection>(canonical.Count);
        foreach (var section in canonical)
        {
            var tmdbIdsByContentId = await ResolveTmdbIdsByContentIdsAsync(section.Items, cancellationToken);
            var localizedItems = await Task.WhenAll(
                section.Items.Select(item => LocalizeRecommendationItemAsync(
                    item,
                    tmdbIdsByContentId,
                    contentLocale,
                    cancellationToken)));

            localizedSections.Add(section with { Items = localizedItems });
        }

        return localizedSections;
    }

    private async Task<IReadOnlyList<HomeItem>> ApplyToHomeItemsAsync(
        IReadOnlyList<HomeItem> items,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var movieIds = items
            .Where(item => string.Equals(item.ContentType, "movie", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();
        var tvIds = items
            .Where(item => string.Equals(item.ContentType, "tv", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();

        var movieTmdbIds = await movieRepository.GetTmdbIdsByIdsAsync(movieIds, cancellationToken);
        var tvTmdbIds = await tvShowRepository.GetTmdbIdsByIdsAsync(tvIds, cancellationToken);

        return await Task.WhenAll(
            items.Select(item => LocalizeHomeItemAsync(
                item,
                movieTmdbIds,
                tvTmdbIds,
                contentLocale,
                cancellationToken)));
    }

    private async Task<SearchSuggestion> LocalizeSuggestionAsync(
        SearchSuggestion suggestion,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (suggestion.TmdbId is not > 0)
        {
            return suggestion;
        }

        var localizedTitle = await ResolveLocalizedTitleAsync(
            suggestion.Type,
            suggestion.TmdbId.Value,
            suggestion.Title,
            contentLocale,
            cancellationToken);

        return suggestion with { Title = localizedTitle };
    }

    private async Task<RecommendationItem> LocalizeRecommendationItemAsync(
        RecommendationItem item,
        IReadOnlyDictionary<Guid, int> tmdbIdsByContentId,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (!tmdbIdsByContentId.TryGetValue(item.Id, out var tmdbId))
        {
            return item;
        }

        var localizedFields = await ResolveLocalizedFieldsAsync(
            item.Type,
            tmdbId,
            item.Title,
            item.Overview,
            contentLocale,
            cancellationToken);

        return item with
        {
            Title = localizedFields.Title,
            Overview = localizedFields.Overview
        };
    }

    private async Task<HomeItem> LocalizeHomeItemAsync(
        HomeItem item,
        IReadOnlyDictionary<Guid, int> movieTmdbIds,
        IReadOnlyDictionary<Guid, int> tvTmdbIds,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(item.ContentType, "movie", StringComparison.OrdinalIgnoreCase) &&
            movieTmdbIds.TryGetValue(item.Id, out var movieTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "movie",
                movieTmdbId,
                item.Title,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle };
        }

        if (string.Equals(item.ContentType, "tv", StringComparison.OrdinalIgnoreCase) &&
            tvTmdbIds.TryGetValue(item.Id, out var tvTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "tv",
                tvTmdbId,
                item.Title,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle };
        }

        return item;
    }

    private async Task<CatalogUpcomingItemResult> LocalizeUpcomingItemAsync(
        CatalogUpcomingItemResult item,
        IReadOnlyDictionary<Guid, int> movieTmdbIds,
        IReadOnlyDictionary<Guid, int> tvTmdbIds,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (item.ContentType == Domain.Enums.CatalogContentType.Movie &&
            movieTmdbIds.TryGetValue(item.ContentId, out var movieTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "movie",
                movieTmdbId,
                item.Title,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle };
        }

        if (item.ContentType == Domain.Enums.CatalogContentType.Tv &&
            tvTmdbIds.TryGetValue(item.ContentId, out var tvTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "tv",
                tvTmdbId,
                item.Title,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle };
        }

        return item;
    }

    private async Task<SearchItem> ApplyToSearchItemAsync(
        SearchItem item,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (item.TmdbId is not > 0)
        {
            return item;
        }

        var localizedFields = await ResolveLocalizedFieldsAsync(
            item.Type,
            item.TmdbId.Value,
            item.Title,
            item.Overview,
            contentLocale,
            cancellationToken);

        return item with
        {
            Title = localizedFields.Title,
            Overview = localizedFields.Overview
        };
    }

    private async Task<(string Title, string? Overview)> ResolveLocalizedFieldsAsync(
        string contentType,
        int tmdbId,
        string canonicalTitle,
        string? canonicalOverview,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(contentType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await TryGetCachedMovieOverlayAsync(tmdbId, contentLocale, cancellationToken);
            return (
                LocalizationFieldFallback.Choose(canonicalTitle, overlay?.Title),
                LocalizationFieldFallback.ChooseNullable(canonicalOverview, overlay?.Overview));
        }

        if (string.Equals(contentType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await TryGetCachedTvShowOverlayAsync(tmdbId, contentLocale, cancellationToken);
            return (
                LocalizationFieldFallback.Choose(canonicalTitle, overlay?.Title),
                LocalizationFieldFallback.ChooseNullable(canonicalOverview, overlay?.Overview));
        }

        return (canonicalTitle, canonicalOverview);
    }

    private async Task<string> ResolveLocalizedTitleAsync(
        string contentType,
        int tmdbId,
        string canonicalTitle,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(contentType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await TryGetCachedMovieOverlayAsync(tmdbId, contentLocale, cancellationToken);
            return LocalizationFieldFallback.Choose(canonicalTitle, overlay?.Title);
        }

        if (string.Equals(contentType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await TryGetCachedTvShowOverlayAsync(tmdbId, contentLocale, cancellationToken);
            return LocalizationFieldFallback.Choose(canonicalTitle, overlay?.Title);
        }

        return canonicalTitle;
    }

    private async Task<MovieDetailLocalizationData?> TryGetCachedMovieOverlayAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var cached = await cacheService.GetAsync<DetailLocalizationCacheEntry<MovieDetailLocalizationData>>(
            DetailLocalizationCacheKeys.Movie(tmdbId, contentLocale),
            cancellationToken);

        return cached?.Data;
    }

    private async Task<TvShowDetailLocalizationData?> TryGetCachedTvShowOverlayAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        var cached = await cacheService.GetAsync<DetailLocalizationCacheEntry<TvShowDetailLocalizationData>>(
            DetailLocalizationCacheKeys.TvShow(tmdbId, contentLocale),
            cancellationToken);

        return cached?.Data;
    }

    private async Task<IReadOnlyDictionary<Guid, int>> ResolveTmdbIdsByContentIdsAsync(
        IReadOnlyList<RecommendationItem> items,
        CancellationToken cancellationToken)
    {
        var movieIds = items
            .Where(item => string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();
        var tvIds = items
            .Where(item => string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToList();

        var movieTmdbIds = await movieRepository.GetTmdbIdsByIdsAsync(movieIds, cancellationToken);
        var tvTmdbIds = await tvShowRepository.GetTmdbIdsByIdsAsync(tvIds, cancellationToken);

        var combined = new Dictionary<Guid, int>(movieTmdbIds.Count + tvTmdbIds.Count);
        foreach (var pair in movieTmdbIds)
        {
            combined[pair.Key] = pair.Value;
        }

        foreach (var pair in tvTmdbIds)
        {
            combined[pair.Key] = pair.Value;
        }

        return combined;
    }

    public async Task<IReadOnlyList<CatalogUpcomingItemResult>> ApplyToUpcomingItemsAsync(
        IReadOnlyList<CatalogUpcomingItemResult> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Count == 0)
        {
            return canonical;
        }

        var movieIds = canonical
            .Where(item => item.ContentType == Domain.Enums.CatalogContentType.Movie)
            .Select(item => item.ContentId)
            .ToList();
        var tvIds = canonical
            .Where(item => item.ContentType == Domain.Enums.CatalogContentType.Tv)
            .Select(item => item.ContentId)
            .ToList();

        var movieTmdbIds = await movieRepository.GetTmdbIdsByIdsAsync(movieIds, cancellationToken);
        var tvTmdbIds = await tvShowRepository.GetTmdbIdsByIdsAsync(tvIds, cancellationToken);

        return await Task.WhenAll(
            canonical.Select(item => LocalizeUpcomingItemAsync(
                item,
                movieTmdbIds,
                tvTmdbIds,
                contentLocale,
                cancellationToken)));
    }
}
