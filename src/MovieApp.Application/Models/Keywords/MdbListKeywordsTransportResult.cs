using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Keywords;

public sealed record MdbListKeywordsTransportResult(
    CatalogContentType MediaType,
    int TmdbId,
    IReadOnlyList<MdbListKeywordTransportItem> Keywords,
    bool IsNotFound);
