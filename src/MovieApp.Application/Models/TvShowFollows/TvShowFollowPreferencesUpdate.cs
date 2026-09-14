namespace MovieApp.Application.Models.TvShowFollows;

public sealed record TvShowFollowPreferencesUpdate(
    bool? NotifyNewSeasons,
    bool? NotifyNewEpisodes);
