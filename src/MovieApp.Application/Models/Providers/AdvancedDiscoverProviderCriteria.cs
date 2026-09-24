using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Providers;

public sealed record AdvancedDiscoverProviderCriteria(
    int Page,
    IReadOnlyList<int> GenreTmdbIds,
    GenreMatchMode GenreMatch,
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
    string? Certification,
    string? CertificationCountry,
    IReadOnlyList<DiscoverReleaseType> ReleaseTypes,
    string? WatchRegion,
    IReadOnlyList<int> WatchProviderIds,
    IReadOnlyList<WatchMonetizationType> WatchMonetizationTypes,
    AdvancedDiscoverSort Sort);
