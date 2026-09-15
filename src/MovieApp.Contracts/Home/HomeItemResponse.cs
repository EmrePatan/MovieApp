namespace MovieApp.Contracts.Home;

public sealed record HomeItemResponse(
    Guid Id,
    string ContentType,
    string Title,
    string? OriginalTitle,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount,
    string? UpcomingKind = null,
    Guid? EpisodeId = null,
    int? SeasonNumber = null,
    int? EpisodeNumber = null,
    string? EpisodeName = null);
