namespace MovieApp.Application.Models.TvShowFollows;

public sealed record TvShowFollowStatusResult(
    bool IsFollowing,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished);
