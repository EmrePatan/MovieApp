using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Search;

public sealed record ContentSearchTitleEnrichmentCandidate(
    CatalogContentType ContentType,
    Guid ContentId,
    int? TmdbId,
    string DisplayTitle);
