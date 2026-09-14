using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.CatalogFollows;

public sealed record CatalogFollowItemResult(
    Guid ContentId,
    CatalogContentType ContentType,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    bool NotifyMovieRelease,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished,
    DateTime FollowedAt);
