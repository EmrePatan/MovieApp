namespace MovieApp.Contracts.TvShowFollows;

public sealed record TvShowFollowsResponse(
    IReadOnlyList<TvShowFollowItemResponse> TvShows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record TvShowFollowItemResponse(
    Guid Id,
    string Title,
    string? PosterUrl,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished,
    DateTime FollowedAt);
