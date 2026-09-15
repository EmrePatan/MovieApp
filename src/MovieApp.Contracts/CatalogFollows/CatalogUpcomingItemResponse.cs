namespace MovieApp.Contracts.CatalogFollows;

public sealed record CatalogUpcomingItemResponse(
    Guid ContentId,
    string ContentType,
    string UpcomingKind,
    string Title,
    string? PosterPath,
    DateOnly ReleaseDate,
    bool IsFollowed,
    Guid? EpisodeId = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null,
    string? EpisodeName = null);
