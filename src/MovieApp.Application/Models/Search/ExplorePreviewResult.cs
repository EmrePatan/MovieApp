using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Models.Search;

public sealed record ExplorePreviewResult(
    IReadOnlyList<string> RailOrder,
    PaginatedResult<SearchItem> HiddenGems,
    PaginatedResult<SearchItem> Popular,
    PaginatedResult<SearchItem> NewReleases,
    PaginatedResult<SearchItem> TopRated);
