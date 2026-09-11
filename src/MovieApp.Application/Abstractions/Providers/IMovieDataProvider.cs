using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface IMovieDataProvider
{
    Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        CancellationToken cancellationToken = default);
}
