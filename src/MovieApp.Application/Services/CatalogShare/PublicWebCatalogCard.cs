namespace MovieApp.Application.Services.CatalogShare;

public sealed record PublicWebCatalogCard(
    Guid Id,
    string Title,
    int? Year,
    decimal VoteAverage,
    string? PosterPath,
    string CanonicalDetailUrl);
