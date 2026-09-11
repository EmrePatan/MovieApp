using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public interface ISearchTvShowsService
{
    Task<PaginatedResult<TvShowSearchResult>> SearchAsync(
        TvShowSearchRequest request,
        CancellationToken cancellationToken = default);
}
