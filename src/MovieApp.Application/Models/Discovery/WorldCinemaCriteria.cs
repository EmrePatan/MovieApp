using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Discovery;

public sealed record WorldCinemaCriteria(
    SearchContentType MediaType,
    string OriginCountry,
    AdvancedDiscoverSort Sort,
    int Page,
    int PageSize,
    IReadOnlyList<Guid> GenreIds,
    GenreMatchMode GenreMatch,
    int? YearFrom,
    int? YearTo,
    decimal? MinRating,
    decimal? MaxRating,
    int? MinVoteCount,
    int? MinRuntimeMinutes,
    int? MaxRuntimeMinutes,
    string? OriginalLanguage,
    IReadOnlyList<Guid> KeywordIds,
    IReadOnlyList<TvDiscoverStatus> TvStatuses)
{
    public WorldCinemaCriteria(
        SearchContentType mediaType,
        string originCountry,
        AdvancedDiscoverSort sort,
        int page,
        int pageSize)
        : this(
            mediaType,
            originCountry,
            sort,
            page,
            pageSize,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [])
    {
    }
}
