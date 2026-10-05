using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Models.Localization;

public sealed record SummaryLocalizationMetadataBatch(
    IReadOnlyDictionary<Guid, ContentProductionContext> MovieProductionContexts,
    IReadOnlyDictionary<Guid, ContentProductionContext> TvProductionContexts,
    IReadOnlyDictionary<CatalogContentKey, string> LocalizedMovieTitles,
    IReadOnlyDictionary<CatalogContentKey, string> LocalizedTvTitles,
    IReadOnlyDictionary<ContentLocalizedPosterKey, string> LocalizedPosters);
