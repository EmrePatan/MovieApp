using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRecommendationTmdbSearch
{
    Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        string? language,
        CancellationToken cancellationToken = default);

    Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        string? language,
        CancellationToken cancellationToken = default);
}
