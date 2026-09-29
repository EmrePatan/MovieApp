using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class NullAiRecommendationTmdbSearch : IAiRecommendationTmdbSearch
{
    public Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        string? language,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new MovieProviderSearchResult(
            [],
            page,
            pageSize,
            0,
            0));

    public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        string? language,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new TvShowProviderSearchResult(
            [],
            page,
            pageSize,
            0,
            0));
}
