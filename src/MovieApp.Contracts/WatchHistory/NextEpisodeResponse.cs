namespace MovieApp.Contracts.WatchHistory;

public sealed record NextEpisodeResponse(
    Guid EpisodeId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title);
