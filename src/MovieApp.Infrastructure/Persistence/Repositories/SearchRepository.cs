using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence.Search;
using Microsoft.EntityFrameworkCore;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SearchRepository(ApplicationDbContext dbContext) : ISearchRepository
{
    public async Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
            ? null
            : QueryNormalizer.Normalize(criteria.Query);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, criteria, normalizedQuery);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, criteria.Sort, normalizedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = QueryNormalizer.Normalize(query);
        var searchCriteria = new SearchCriteria(
            query,
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            limit);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, normalizedQuery);

        var projections = await SearchQueryBuilder
            .ApplyRelevanceSort(combinedQuery, normalizedQuery)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return projections
            .Select(item => new SearchSuggestion(item.Id, item.Type, item.Title))
            .ToList();
    }

    public async Task<PaginatedResult<SearchItem>> GetPopularAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.Popular,
            criteria.Page,
            criteria.PageSize);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, null);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, SearchSortOption.Popular, null)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetTrendingAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.Popular,
            criteria.Page,
            criteria.PageSize);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, null);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplyTrendingSort(combinedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var combinedQuery = SearchQueryBuilder.BuildNewReleasesQuery(dbContext, criteria);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplyNewReleasesSort(combinedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var combinedQuery = SearchQueryBuilder.BuildTopRatedQuery(dbContext, criteria);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplyTopRatedSort(combinedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.Popular,
            criteria.Page,
            criteria.PageSize);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, null, genreName);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, SearchSortOption.Popular, null)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    private static PaginatedResult<SearchItem> ToPaginatedResult(
        IReadOnlyList<SearchItemProjection> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchItem>(
            items.Select(ToSearchItem).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static SearchItem ToSearchItem(SearchItemProjection projection) =>
        new(
            projection.Id,
            projection.Type,
            projection.Title,
            projection.OriginalTitle,
            projection.Overview,
            projection.PosterUrl,
            projection.BackdropUrl,
            projection.ReleaseDate,
            projection.VoteAverage,
            projection.VoteCount,
            projection.Year);
}
