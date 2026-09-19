using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
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

        var localizedItems = new List<SearchItem>(canonical.Items.Count);
        foreach (var item in canonical.Items)
        {
            localizedItems.Add(await ApplyToSearchItemAsync(item, contentLocale, cancellationToken));
        }

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

        var localizedSuggestions = new List<SearchSuggestion>(canonical.Count);
        foreach (var suggestion in canonical)
        {
            if (suggestion.TmdbId is not > 0)
            {
                localizedSuggestions.Add(suggestion);
                continue;
            }

            var localizedTitle = await ResolveLocalizedTitleAsync(
                suggestion.Type,
                suggestion.TmdbId.Value,
                suggestion.Title,
                contentLocale,
                cancellationToken);

            localizedSuggestions.Add(suggestion with { Title = localizedTitle });
        }

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
        var localizedItems = new List<RecommendationItem>(canonical.Items.Count);

        foreach (var item in canonical.Items)
        {
            if (!tmdbIdsByContentId.TryGetValue(item.Id, out var tmdbId))
            {
                localizedItems.Add(item);
                continue;
            }

            var localizedTitle = await ResolveLocalizedTitleAsync(
                item.Type,
                tmdbId,
                item.Title,
                contentLocale,
                cancellationToken);
            var localizedOverview = await ResolveLocalizedOverviewAsync(
                item.Type,
                tmdbId,
                item.Overview,
                contentLocale,
                cancellationToken);

            localizedItems.Add(item with
            {
                Title = localizedTitle,
                Overview = localizedOverview
            });
        }

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
            var localizedItems = new List<RecommendationItem>(section.Items.Count);
            var tmdbIdsByContentId = await ResolveTmdbIdsByContentIdsAsync(section.Items, cancellationToken);

            foreach (var item in section.Items)
            {
                if (!tmdbIdsByContentId.TryGetValue(item.Id, out var tmdbId))
                {
                    localizedItems.Add(item);
                    continue;
                }

                var localizedTitle = await ResolveLocalizedTitleAsync(
                    item.Type,
                    tmdbId,
                    item.Title,
                    contentLocale,
                    cancellationToken);
                var localizedOverview = await ResolveLocalizedOverviewAsync(
                    item.Type,
                    tmdbId,
                    item.Overview,
                    contentLocale,
                    cancellationToken);

                localizedItems.Add(item with
                {
                    Title = localizedTitle,
                    Overview = localizedOverview
                });
            }

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

        var localizedItems = new List<HomeItem>(items.Count);
        foreach (var item in items)
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
                localizedItems.Add(item with { Title = localizedTitle });
                continue;
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
                localizedItems.Add(item with { Title = localizedTitle });
                continue;
            }

            localizedItems.Add(item);
        }

        return localizedItems;
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

        var localizedTitle = await ResolveLocalizedTitleAsync(
            item.Type,
            item.TmdbId.Value,
            item.Title,
            contentLocale,
            cancellationToken);
        var localizedOverview = await ResolveLocalizedOverviewAsync(
            item.Type,
            item.TmdbId.Value,
            item.Overview,
            contentLocale,
            cancellationToken);

        return item with
        {
            Title = localizedTitle,
            Overview = localizedOverview
        };
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

    private async Task<string?> ResolveLocalizedOverviewAsync(
        string contentType,
        int tmdbId,
        string? canonicalOverview,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(contentType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await TryGetCachedMovieOverlayAsync(tmdbId, contentLocale, cancellationToken);
            return LocalizationFieldFallback.ChooseNullable(canonicalOverview, overlay?.Overview);
        }

        if (string.Equals(contentType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await TryGetCachedTvShowOverlayAsync(tmdbId, contentLocale, cancellationToken);
            return LocalizationFieldFallback.ChooseNullable(canonicalOverview, overlay?.Overview);
        }

        return canonicalOverview;
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
}
