using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.CatalogFollows;

public sealed record CatalogUpcomingItemResult(
    Guid ContentId,
    CatalogContentType ContentType,
    string Title,
    string? PosterPath,
    DateOnly ReleaseDate,
    bool IsFollowed);
