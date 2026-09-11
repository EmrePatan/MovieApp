namespace MovieApp.Application.Models.WatchHistory;

public sealed record NextEpisodeResult(
    Guid EpisodeId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title);
