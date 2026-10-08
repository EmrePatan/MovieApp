using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Browse criteria for the Keşfet genre rails. Names are the canonical English genre
/// rows; the mobile hub localizes those same rows and requests one popular page.
/// </summary>
public static class GenreHubRailWarmup
{
    public const int Page = 1;

    public const int PageSize = 20;

    /// <summary>
    /// Mobile rail order in <c>GENRE_HUB_RAIL_GENRE_NAMES</c>.
    /// </summary>
    public static readonly string[] CanonicalGenreNames =
    [
        "Science Fiction",
        "Fantasy",
        "Mystery",
        "Action",
        "Drama",
        "Comedy",
        "Romance",
        "War"
    ];

    public static DiscoverBrowseCriteria CreateCriteria(Guid genreId) =>
        new(
            DiscoverBrowseMode.Popular,
            SearchContentType.All,
            [genreId],
            Year: null,
            YearFrom: null,
            YearTo: null,
            MinRating: null,
            MinVoteCount: null,
            MinRuntimeMinutes: null,
            MaxRuntimeMinutes: null,
            Language: null,
            OriginCountry: null,
            KeywordIds: [],
            TvStatuses: [],
            Sort: DiscoverBrowseSort.PopularityDesc,
            Page: Page,
            PageSize: PageSize);
}
