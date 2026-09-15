namespace MovieApp.Application.Models.TvUpcomingEpisodes;

public sealed record TvUpcomingEpisodeSyncBatchResult(
    int Selected,
    int Succeeded,
    int Failed,
    int Hydrated);
