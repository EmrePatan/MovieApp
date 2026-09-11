namespace MovieApp.Application.Models.WatchHistory;

public sealed record SeasonNextEpisodeResult(
    Guid EpisodeId,
    int EpisodeNumber,
    string? Title);
