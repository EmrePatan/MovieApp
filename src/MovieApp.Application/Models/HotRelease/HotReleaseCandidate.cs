namespace MovieApp.Application.Models.HotRelease;

public sealed record HotReleaseCandidate(
    Guid TvShowId,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId);
