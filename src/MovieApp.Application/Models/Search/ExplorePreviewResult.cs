using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Models.Search;

public sealed record ExplorePreviewResult(
    PaginatedResult<SearchItem> Trending,
    PaginatedResult<SearchItem> TopRated,
    PaginatedResult<SearchItem> NewReleases);
