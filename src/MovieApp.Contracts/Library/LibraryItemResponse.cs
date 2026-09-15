namespace MovieApp.Contracts.Library;

public sealed record LibraryItemResponse(
    Guid Id,
    string Type,
    string Title,
    string? OriginalTitle,
    string? PosterUrl,
    string? BackdropUrl,
    int? Year,
    decimal? VoteAverage,
    DateTime? AddedAt,
    DateTime? WatchedAt,
    DateTime? LastActivityAt,
    decimal? ProgressPercentage,
    LibraryNextEpisodeResponse? NextEpisode,
    string CollectionStatus);
