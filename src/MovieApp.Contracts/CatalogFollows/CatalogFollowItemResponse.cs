namespace MovieApp.Contracts.CatalogFollows;

public sealed record CatalogFollowItemResponse(
    Guid ContentId,
    string ContentType,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    bool NotifyMovieRelease,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished,
    DateTime FollowedAt);
