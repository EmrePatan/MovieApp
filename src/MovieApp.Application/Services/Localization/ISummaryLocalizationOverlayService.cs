using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Localization;

public interface ISummaryLocalizationOverlayService
{
    Task<PaginatedResult<SearchItem>> ApplyToSearchItemsAsync(
        PaginatedResult<SearchItem> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Localizes a list page. <see cref="SearchListLocalizationMode.CatalogTitlesOnly"/> keeps the canonical
    /// overview and does not call TMDB detail. Existing implementors keep full overlay behavior.
    /// </summary>
    Task<PaginatedResult<SearchItem>> ApplyToSearchItemsAsync(
        PaginatedResult<SearchItem> canonical,
        string contentLocale,
        SearchListLocalizationMode localizationMode,
        CancellationToken cancellationToken = default) =>
        ApplyToSearchItemsAsync(canonical, contentLocale, cancellationToken);

    Task<IReadOnlyList<SearchSuggestion>> ApplyToSearchSuggestionsAsync(
        IReadOnlyList<SearchSuggestion> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<RecommendationItem>> ApplyToRecommendationItemsAsync(
        PaginatedResult<RecommendationItem> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<HomeResult> ApplyToHomeResultAsync(
        HomeResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationSection>> ApplyToRecommendationSectionsAsync(
        IReadOnlyList<RecommendationSection> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogUpcomingItemResult>> ApplyToUpcomingItemsAsync(
        IReadOnlyList<CatalogUpcomingItemResult> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiValidatedRecommendation>> ApplyToAiValidatedRecommendationsAsync(
        IReadOnlyList<AiValidatedRecommendation> canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
