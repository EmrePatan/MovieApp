namespace MovieApp.Application.Models.TvShowFollows;

public sealed record TvShowFollowsListResult(
    IReadOnlyList<TvShowFollowTvShowResult> TvShows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}

public sealed record TvShowFollowTvShowResult(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished,
    DateTime FollowedAt);
