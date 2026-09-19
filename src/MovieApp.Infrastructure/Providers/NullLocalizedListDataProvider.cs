using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

internal sealed class NullLocalizedListDataProvider : ILocalizedListDataProvider
{
    public Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyMovieResult(page));

    public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyTvResult(page));

    public Task<PersonProviderSearchResult> SearchPersonsAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PersonProviderSearchResult([], page, TmdbSearchDefaults.ResultsPerPage, 0, 0));

    public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyMovieResult(criteria.Page));

    public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyTvResult(criteria.Page));

    public Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyMovieResult(criteria.Page));

    public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(EmptyTvResult(criteria.Page));

    private static MovieProviderSearchResult EmptyMovieResult(int page) =>
        new([], page, TmdbSearchDefaults.ResultsPerPage, 0, 0);

    private static TvShowProviderSearchResult EmptyTvResult(int page) =>
        new([], page, TmdbSearchDefaults.ResultsPerPage, 0, 0);
}
