namespace MovieApp.Contracts.WatchHistory;

public sealed record SeasonNextEpisodeResponse(
    Guid EpisodeId,
    int EpisodeNumber,
    string? Title);
