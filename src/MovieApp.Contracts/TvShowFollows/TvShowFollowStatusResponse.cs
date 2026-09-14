namespace MovieApp.Contracts.TvShowFollows;

public sealed record TvShowFollowStatusResponse(
    bool IsFollowing,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished);
