namespace MovieApp.Contracts.Discovery;

public sealed record LeavingSoonResponse(
    IReadOnlyList<LeavingSoonItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record LeavingSoonItemResponse(
    Guid Id,
    string Type,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    int VoteCount,
    int? Year,
    int? TmdbId,
    IReadOnlyList<string> Genres,
    DateTime LeavesAtUtc,
    IReadOnlyList<LeavingSoonPlatformResponse> Platforms);

public sealed record LeavingSoonPlatformResponse(
    string ServiceId,
    string Name,
    DateTime LeavesAtUtc,
    string? WatchLink);
