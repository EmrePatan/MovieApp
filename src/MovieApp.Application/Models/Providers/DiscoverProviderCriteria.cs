using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Providers;

public sealed record DiscoverProviderCriteria(
    DiscoverBrowseMode Mode,
    int Page,
    IReadOnlyList<int> GenreTmdbIds,
    int? Year,
    int? YearFrom,
    int? YearTo,
    decimal? MinRating,
    int? MinVoteCount,
    int? MinRuntimeMinutes,
    int? MaxRuntimeMinutes,
    string? Language,
    string? OriginCountry,
    IReadOnlyList<int> KeywordTmdbIds,
    IReadOnlyList<TvDiscoverStatus> TvStatuses,
    DiscoverBrowseSort? Sort);
