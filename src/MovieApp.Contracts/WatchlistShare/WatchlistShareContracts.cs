namespace MovieApp.Contracts.WatchlistShare;

public sealed record WatchlistShareStatusResponse(bool IsSharingEnabled);

public sealed record WatchlistShareEnableResponse(string? ShareUrl, bool CreatedNewLink);

public sealed record WatchlistShareRotateResponse(string ShareUrl);

public sealed record WatchlistShareSummaryResponse(Guid WatchlistId, string WatchlistName);

public sealed record WatchlistShareListResponse(IReadOnlyList<WatchlistShareSummaryResponse> Items);

public sealed record PublicWatchlistShareItemResponse(
    string ContentType,
    Guid ContentId,
    string Title,
    int? Year,
    string? PosterPath,
    decimal VoteAverage);

public sealed record PublicWatchlistShareResponse(
    string? OwnerDisplayName,
    string? WatchlistName,
    IReadOnlyList<PublicWatchlistShareItemResponse> Items);
