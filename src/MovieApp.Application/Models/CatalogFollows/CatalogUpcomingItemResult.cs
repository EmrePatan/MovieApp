using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.CatalogFollows;

public sealed record CatalogUpcomingItemResult(
    Guid ContentId,
    CatalogContentType ContentType,
    CatalogUpcomingKind UpcomingKind,
    string Title,
    string? PosterPath,
    DateOnly ReleaseDate,
    bool IsFollowed,
    Guid? EpisodeId = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null,
    string? EpisodeName = null);
