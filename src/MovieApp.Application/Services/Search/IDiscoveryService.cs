using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IDiscoveryService
{
    Task<PaginatedResult<SearchItem>> GetPopularAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetTrendingAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keşfet new-releases rail. Uses catalog display titles and does not fetch TMDB detail overviews.
    /// Home keeps <see cref="GetNewReleasesAsync"/>.
    /// </summary>
    Task<PaginatedResult<SearchItem>> GetCatalogListNewReleasesAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        GetNewReleasesAsync(criteria, contentLocale, cancellationToken);

    Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Keşfet top-rated rail. Uses catalog display titles and does not fetch TMDB detail overviews.
    /// Home keeps <see cref="GetTopRatedAsync"/>.
    /// </summary>
    Task<PaginatedResult<SearchItem>> GetCatalogListTopRatedAsync(
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        GetTopRatedAsync(criteria, contentLocale, cancellationToken);

    Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetHiddenGemsAsync(
        DiscoverBrowseCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromException<PaginatedResult<SearchItem>>(
            new NotSupportedException("Hidden gems is not available on this discovery service."));

    Task<PaginatedResult<SearchItem>> GetTopRatedBrowseAsync(
        DiscoverBrowseCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        GetTopRatedAsync(
            new DiscoveryCriteria(criteria.Type, criteria.Page, criteria.PageSize),
            contentLocale,
            cancellationToken);
}
