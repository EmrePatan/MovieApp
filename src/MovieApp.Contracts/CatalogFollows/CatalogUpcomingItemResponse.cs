namespace MovieApp.Contracts.CatalogFollows;

public sealed record CatalogUpcomingItemResponse(
    Guid ContentId,
    string ContentType,
    string Title,
    string? PosterPath,
    DateOnly ReleaseDate,
    bool IsFollowed);
