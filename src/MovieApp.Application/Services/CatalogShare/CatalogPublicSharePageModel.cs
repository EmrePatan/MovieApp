namespace MovieApp.Application.Services.CatalogShare;

public enum CatalogShareContentKind
{
    Movie,
    TvShow,
}

public sealed record CatalogPublicSharePageModel(
    CatalogShareContentKind ContentKind,
    Guid ContentId,
    string Title,
    string? Overview,
    int? Year,
    IReadOnlyList<string> Genres,
    string? PosterPath,
    decimal? VoteAverage,
    string CanonicalPageUrl,
    string OpenInAppUrl);
