using MovieApp.Application.Models.Search;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.StreamingLeaving;

public sealed record StreamingService(string Id, string Name);

public readonly record struct StreamingTitleKey(CatalogContentType MediaType, int TmdbId);

public sealed record StreamingLeavingChange(
    string ServiceId,
    StreamingTitleKey Title,
    DateTime LeavesAtUtc,
    string? WatchLink);

public sealed record StreamingLeavingPage(
    IReadOnlyList<StreamingLeavingChange> Changes,
    string? NextCursor);

public sealed record StreamingLeavingRow(
    string ServiceId,
    string ServiceName,
    string? WatchLink,
    DateTime LeavesAtUtc,
    CatalogContentType MediaType,
    Guid ContentId,
    int TmdbId,
    string Title,
    string? OriginalTitle,
    string? Overview,
    string? PosterPath,
    string? BackdropPath,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? OriginalLanguage,
    string? OriginCountryCode,
    decimal VoteAverage,
    int VoteCount,
    IReadOnlyList<Guid> GenreIds);

public enum StreamingLeavingSort
{
    LeavingAsc,
    RatingDesc,
    RatingAsc,
    ReleaseDesc,
    ReleaseAsc,
    TitleAsc,
    TitleDesc
}

public sealed record StreamingLeavingCriteria(
    string Region,
    SearchContentType MediaType,
    int Page,
    int PageSize,
    IReadOnlyList<Guid> GenreIds,
    bool MatchAllGenres,
    int? YearFrom,
    int? YearTo,
    decimal? MinRating,
    decimal? MaxRating,
    int? MinVoteCount,
    int? MinRuntimeMinutes,
    int? MaxRuntimeMinutes,
    string? OriginalLanguage,
    string? OriginCountry,
    IReadOnlyList<int> WatchProviderIds,
    StreamingLeavingSort Sort);

public sealed record StreamingLeavingPlatform(
    string ServiceId,
    string Name,
    DateTime LeavesAtUtc,
    string? WatchLink);

public sealed record StreamingLeavingResultItem(
    SearchItem Item,
    DateTime LeavesAtUtc,
    IReadOnlyList<StreamingLeavingPlatform> Platforms);

public sealed record StreamingLeavingResult(
    IReadOnlyList<StreamingLeavingResultItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static StreamingLeavingResult Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
