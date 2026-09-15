namespace MovieApp.Application.Models.Search;

public sealed record AdvancedDiscoverCriteria(
    SearchContentType MediaType,
    IReadOnlyList<Guid> GenreIds,
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
    string? WatchRegion,
    IReadOnlyList<int> WatchProviderIds,
    IReadOnlyList<WatchMonetizationType> WatchMonetizationTypes,
    AdvancedDiscoverSort Sort,
    int Page,
    int PageSize);
