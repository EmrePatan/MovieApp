using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Movies;

public interface ISearchMoviesService
{
    Task<PaginatedResult<MovieSearchResult>> SearchAsync(
        MovieSearchRequest request,
        CancellationToken cancellationToken = default);
}
