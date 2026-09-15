using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Providers;

public sealed record AdvancedDiscoverProviderCriteria(
    int Page,
    IReadOnlyList<int> GenreTmdbIds,
    int? Year,
    int? YearFrom,
    int? YearTo,
    decimal? MinRating,
    decimal? MaxRating,
    int? MinVoteCount,
    int? MinRuntimeMinutes,
    int? MaxRuntimeMinutes,
    string? OriginalLanguage,
    string? OriginCountry,
    AdvancedDiscoverSort Sort);
