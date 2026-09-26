namespace MovieApp.Application.Models.Library;

public sealed record LibraryItemResult(
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
    LibraryNextEpisodeResult? NextEpisode,
    string CollectionStatus,
    bool? WatchingSortInProgress = null);
