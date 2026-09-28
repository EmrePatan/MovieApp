namespace MovieApp.Application.Models.WatchlistShare;

public sealed record PublicWatchlistShareItemResult(
    string ContentType,
    Guid ContentId,
    string Title,
    int? Year,
    string? PosterPath,
    decimal VoteAverage);

public sealed record PublicWatchlistShareResult(
    string? OwnerDisplayName,
    string? WatchlistName,
    IReadOnlyList<PublicWatchlistShareItemResult> Items);
