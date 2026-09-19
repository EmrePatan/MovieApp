using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface ILocalizedListDataProvider
{
    Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PersonProviderSearchResult> SearchPersonsAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
