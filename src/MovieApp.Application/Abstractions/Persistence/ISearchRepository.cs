using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ISearchRepository
{
    Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetPopularAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetTrendingAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default);
}
