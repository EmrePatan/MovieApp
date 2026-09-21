using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface IMovieDataProvider
{
    Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        bool includeKeywords = false,
        CancellationToken cancellationToken = default);
}
