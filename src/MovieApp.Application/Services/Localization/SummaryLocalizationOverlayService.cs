using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Domain.Enums;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Localization;

public sealed class SummaryLocalizationOverlayService(
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IDetailLocalizationOverlayService detailLocalizationOverlayService,
    IContentLocalizedPosterRepository contentLocalizedPosterRepository) : ISummaryLocalizationOverlayService
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

        var localizedPosters = await LoadLocalizedPostersForSearchItemsAsync(
            canonical.Items,
            contentLocale,
            cancellationToken);

        var localizedItems = await Task.WhenAll(
            canonical.Items.Select(item => ApplyToSearchItemAsync(
                item,
                contentLocale,
                localizedPosters,
                cancellationToken)));

        return canonical with { Items = localizedItems };
    }

    public async Task<IReadOnlyList<AiValidatedRecommendation>> ApplyToAiValidatedRecommendationsAsync(
        IReadOnlyList<AiValidatedRecommendation> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || canonical.Count == 0)
        {
            return canonical;
        }

        var localizedPosters = await LoadLocalizedPostersForAiRecommendationsAsync(
            canonical,
            contentLocale,
            cancellationToken);

        var localizedRecommendations = await Task.WhenAll(
            canonical.Select(recommendation =>
                LocalizeAiValidatedRecommendationAsync(
                    recommendation,
                    contentLocale,
                    localizedPosters,
                    cancellationToken)));

        return localizedRecommendations;
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
        var localizedPosters = await LoadLocalizedPostersForRecommendationItemsAsync(
            canonical.Items,
            contentLocale,
            cancellationToken);
        var localizedItems = await Task.WhenAll(
            canonical.Items.Select(item => LocalizeRecommendationItemAsync(
                item,
                tmdbIdsByContentId,
                contentLocale,
                localizedPosters,
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
            var localizedPosters = await LoadLocalizedPostersForRecommendationItemsAsync(
                section.Items,
                contentLocale,
                cancellationToken);
            var localizedItems = await Task.WhenAll(
                section.Items.Select(item => LocalizeRecommendationItemAsync(
                    item,
                    tmdbIdsByContentId,
                    contentLocale,
                    localizedPosters,
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
        var localizedPosters = await LoadLocalizedPostersForHomeItemsAsync(items, contentLocale, cancellationToken);

        return await Task.WhenAll(
            items.Select(item => LocalizeHomeItemAsync(
                item,
                movieTmdbIds,
                tvTmdbIds,
                contentLocale,
                localizedPosters,
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
            originalTitle: null,
            originalLanguage: null,
            contentLocale,
            cancellationToken);

        return suggestion with { Title = localizedTitle };
    }

    private async Task<RecommendationItem> LocalizeRecommendationItemAsync(
        RecommendationItem item,
        IReadOnlyDictionary<Guid, int> tmdbIdsByContentId,
        string contentLocale,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string> localizedPosters,
        CancellationToken cancellationToken)
    {
        var posterKey = CreatePosterKey(item.Type, item.Id);
        var posterUrl = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            item.PosterUrl,
            posterKey,
            localizedPosters);

        if (!tmdbIdsByContentId.TryGetValue(item.Id, out var tmdbId))
        {
            return item with { PosterUrl = posterUrl };
        }

        var localizedFields = await ResolveLocalizedFieldsAsync(
            item.Type,
            tmdbId,
            item.Title,
            item.OriginalTitle,
            originalLanguage: null,
            item.Overview,
            contentLocale,
            cancellationToken);

        return item with
        {
            Title = localizedFields.Title,
            Overview = localizedFields.Overview,
            PosterUrl = posterUrl
        };
    }

    private async Task<HomeItem> LocalizeHomeItemAsync(
        HomeItem item,
        IReadOnlyDictionary<Guid, int> movieTmdbIds,
        IReadOnlyDictionary<Guid, int> tvTmdbIds,
        string contentLocale,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string> localizedPosters,
        CancellationToken cancellationToken)
    {
        var posterKey = CreatePosterKey(item.ContentType, item.Id);
        var posterUrl = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            item.PosterUrl,
            posterKey,
            localizedPosters);

        if (string.Equals(item.ContentType, "movie", StringComparison.OrdinalIgnoreCase) &&
            movieTmdbIds.TryGetValue(item.Id, out var movieTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "movie",
                movieTmdbId,
                item.Title,
                item.OriginalTitle,
                originalLanguage: null,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle, PosterUrl = posterUrl };
        }

        if (string.Equals(item.ContentType, "tv", StringComparison.OrdinalIgnoreCase) &&
            tvTmdbIds.TryGetValue(item.Id, out var tvTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "tv",
                tvTmdbId,
                item.Title,
                item.OriginalTitle,
                originalLanguage: null,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle, PosterUrl = posterUrl };
        }

        return item with { PosterUrl = posterUrl };
    }

    private async Task<CatalogUpcomingItemResult> LocalizeUpcomingItemAsync(
        CatalogUpcomingItemResult item,
        IReadOnlyDictionary<Guid, int> movieTmdbIds,
        IReadOnlyDictionary<Guid, int> tvTmdbIds,
        string contentLocale,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string> localizedPosters,
        CancellationToken cancellationToken)
    {
        var posterKey = CreatePosterKey(item.ContentType, item.ContentId);
        var posterPath = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            item.PosterPath,
            posterKey,
            localizedPosters);

        if (item.ContentType == Domain.Enums.CatalogContentType.Movie &&
            movieTmdbIds.TryGetValue(item.ContentId, out var movieTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "movie",
                movieTmdbId,
                item.Title,
                originalTitle: null,
                originalLanguage: null,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle, PosterPath = posterPath };
        }

        if (item.ContentType == Domain.Enums.CatalogContentType.Tv &&
            tvTmdbIds.TryGetValue(item.ContentId, out var tvTmdbId))
        {
            var localizedTitle = await ResolveLocalizedTitleAsync(
                "tv",
                tvTmdbId,
                item.Title,
                originalTitle: null,
                originalLanguage: null,
                contentLocale,
                cancellationToken);
            return item with { Title = localizedTitle, PosterPath = posterPath };
        }

        return item with { PosterPath = posterPath };
    }

    private async Task<AiValidatedRecommendation> LocalizeAiValidatedRecommendationAsync(
        AiValidatedRecommendation recommendation,
        string contentLocale,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string> localizedPosters,
        CancellationToken cancellationToken)
    {
        var posterKey = CreatePosterKey(recommendation.Movie.MediaType, recommendation.Movie.MovieId);
        var posterUrl = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            recommendation.Movie.PosterUrl,
            posterKey,
            localizedPosters);

        if (recommendation.Movie.TmdbId is not int tmdbId || tmdbId <= 0)
        {
            return recommendation with
            {
                Movie = recommendation.Movie with { PosterUrl = posterUrl }
            };
        }

        var localizedFields = await ResolveLocalizedFieldsAsync(
            recommendation.Movie.MediaType,
            tmdbId,
            recommendation.Movie.Title,
            originalTitle: null,
            originalLanguage: null,
            recommendation.Movie.Overview,
            contentLocale,
            cancellationToken);

        return recommendation with
        {
            Movie = recommendation.Movie with
            {
                Title = localizedFields.Title,
                Overview = localizedFields.Overview,
                PosterUrl = posterUrl
            }
        };
    }

    private async Task<SearchItem> ApplyToSearchItemAsync(
        SearchItem item,
        string contentLocale,
        IReadOnlyDictionary<ContentLocalizedPosterKey, string> localizedPosters,
        CancellationToken cancellationToken)
    {
        var posterKey = CreatePosterKey(item.Type, item.Id);
        var posterUrl = LocalizedPosterDisplayOverlay.ChooseDisplayPosterUrl(
            item.PosterUrl,
            posterKey,
            localizedPosters);

        if (item.TmdbId is not > 0)
        {
            return item with { PosterUrl = posterUrl };
        }

        var localizedFields = await ResolveLocalizedFieldsAsync(
            item.Type,
            item.TmdbId.Value,
            item.Title,
            item.OriginalTitle,
            originalLanguage: null,
            item.Overview,
            contentLocale,
            cancellationToken);

        return item with
        {
            Title = localizedFields.Title,
            Overview = localizedFields.Overview,
            PosterUrl = posterUrl
        };
    }

    private async Task<(string Title, string? Overview)> ResolveLocalizedFieldsAsync(
        string contentType,
        int tmdbId,
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string? canonicalOverview,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(contentType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await detailLocalizationOverlayService.LoadMovieOverlayAsync(
                tmdbId,
                contentLocale,
                cancellationToken);
            return (
                LocalizedDisplayTitleSelector.Choose(
                    canonicalTitle,
                    originalTitle,
                    originalLanguage,
                    overlay?.Title,
                    contentLocale),
                LocalizationFieldFallback.ChooseNullable(canonicalOverview, overlay?.Overview));
        }

        if (string.Equals(contentType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await detailLocalizationOverlayService.LoadTvShowOverlayAsync(
                tmdbId,
                contentLocale,
                cancellationToken);
            return (
                LocalizedDisplayTitleSelector.Choose(
                    canonicalTitle,
                    originalTitle,
                    originalLanguage,
                    overlay?.Title,
                    contentLocale),
                LocalizationFieldFallback.ChooseNullable(canonicalOverview, overlay?.Overview));
        }

        return (canonicalTitle, canonicalOverview);
    }

    private async Task<string> ResolveLocalizedTitleAsync(
        string contentType,
        int tmdbId,
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(contentType, "movie", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await detailLocalizationOverlayService.LoadMovieOverlayAsync(
                tmdbId,
                contentLocale,
                cancellationToken);
            return LocalizedDisplayTitleSelector.Choose(
                canonicalTitle,
                originalTitle,
                originalLanguage,
                overlay?.Title,
                contentLocale);
        }

        if (string.Equals(contentType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            var overlay = await detailLocalizationOverlayService.LoadTvShowOverlayAsync(
                tmdbId,
                contentLocale,
                cancellationToken);
            return LocalizedDisplayTitleSelector.Choose(
                canonicalTitle,
                originalTitle,
                originalLanguage,
                overlay?.Title,
                contentLocale);
        }

        return canonicalTitle;
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
        var localizedPosters = await LoadLocalizedPostersForUpcomingItemsAsync(
            canonical,
            contentLocale,
            cancellationToken);

        return await Task.WhenAll(
            canonical.Select(item => LocalizeUpcomingItemAsync(
                item,
                movieTmdbIds,
                tvTmdbIds,
                contentLocale,
                localizedPosters,
                cancellationToken)));
    }

    private static ContentLocalizedPosterKey CreatePosterKey(string contentType, Guid contentId) =>
        string.Equals(contentType, "tv", StringComparison.OrdinalIgnoreCase)
            ? new ContentLocalizedPosterKey(CatalogContentType.Tv, contentId)
            : new ContentLocalizedPosterKey(CatalogContentType.Movie, contentId);

    private static ContentLocalizedPosterKey CreatePosterKey(CatalogContentType contentType, Guid contentId) =>
        new(contentType, contentId);

    private async Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadLocalizedPostersAsync(
        IReadOnlyList<ContentLocalizedPosterKey> keys,
        string contentLocale,
        CancellationToken cancellationToken) =>
        await LocalizedPosterDisplayOverlay.LoadPosterPathsAsync(
            contentLocalizedPosterRepository,
            keys,
            contentLocale,
            cancellationToken);

    private Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadLocalizedPostersForSearchItemsAsync(
        IReadOnlyList<SearchItem> items,
        string contentLocale,
        CancellationToken cancellationToken) =>
        LoadLocalizedPostersAsync(
            items.Select(item => CreatePosterKey(item.Type, item.Id)).ToList(),
            contentLocale,
            cancellationToken);

    private Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadLocalizedPostersForRecommendationItemsAsync(
        IReadOnlyList<RecommendationItem> items,
        string contentLocale,
        CancellationToken cancellationToken) =>
        LoadLocalizedPostersAsync(
            items.Select(item => CreatePosterKey(item.Type, item.Id)).ToList(),
            contentLocale,
            cancellationToken);

    private Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadLocalizedPostersForHomeItemsAsync(
        IReadOnlyList<HomeItem> items,
        string contentLocale,
        CancellationToken cancellationToken) =>
        LoadLocalizedPostersAsync(
            items.Select(item => CreatePosterKey(item.ContentType, item.Id)).ToList(),
            contentLocale,
            cancellationToken);

    private Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadLocalizedPostersForAiRecommendationsAsync(
        IReadOnlyList<AiValidatedRecommendation> items,
        string contentLocale,
        CancellationToken cancellationToken) =>
        LoadLocalizedPostersAsync(
            items.Select(item => CreatePosterKey(item.Movie.MediaType, item.Movie.MovieId)).ToList(),
            contentLocale,
            cancellationToken);

    private Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> LoadLocalizedPostersForUpcomingItemsAsync(
        IReadOnlyList<CatalogUpcomingItemResult> items,
        string contentLocale,
        CancellationToken cancellationToken) =>
        LoadLocalizedPostersAsync(
            items.Select(item => CreatePosterKey(item.ContentType, item.ContentId)).ToList(),
            contentLocale,
            cancellationToken);
}
