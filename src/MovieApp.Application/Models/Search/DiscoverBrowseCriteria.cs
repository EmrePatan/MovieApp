namespace MovieApp.Application.Models.Search;

public sealed record DiscoverBrowseCriteria(
    DiscoverBrowseMode Mode,
    SearchContentType Type,
    IReadOnlyList<Guid> GenreIds,
    int? Year,
    int? YearFrom,
    int? YearTo,
    decimal? MinRating,
    int? MinVoteCount,
    int? MinRuntimeMinutes,
    int? MaxRuntimeMinutes,
    string? Language,
    string? OriginCountry,
    IReadOnlyList<Guid> KeywordIds,
    IReadOnlyList<TvDiscoverStatus> TvStatuses,
    DiscoverBrowseSort? Sort,
    int Page,
    int PageSize);
