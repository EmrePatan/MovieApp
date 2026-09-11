namespace MovieApp.Application.Models.WatchHistory;

public sealed record WatchedEpisodeResult(
    Guid EpisodeId,
    Guid TvShowId,
    Guid SeasonId,
    string TvShowTitle,
    int SeasonNumber,
    int EpisodeNumber,
    string? EpisodeTitle,
    DateTime WatchedAt);
