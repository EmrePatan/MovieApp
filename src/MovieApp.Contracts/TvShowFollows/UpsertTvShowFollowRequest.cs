namespace MovieApp.Contracts.TvShowFollows;

public sealed record UpsertTvShowFollowRequest(
    bool? NotifyNewSeasons,
    bool? NotifyNewEpisodes);
