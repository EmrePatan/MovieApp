using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class UnifiedSearchProviderPolicyTests
{
    [Fact]
    public void IsProviderScopeReturnsTrueForRelevanceQueryWithoutFilters()
    {
        var criteria = CreateCriteria("friends");

        Assert.True(UnifiedSearchProviderPolicy.IsProviderScope(criteria));
    }

    [Fact]
    public void IsProviderScopeReturnsFalseWithoutQuery()
    {
        var criteria = new SearchCriteria(
            null,
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        Assert.False(UnifiedSearchProviderPolicy.IsProviderScope(criteria));
    }

    [Fact]
    public void IsProviderScopeReturnsFalseForNonRelevanceSort()
    {
        var criteria = new SearchCriteria(
            "friends",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.DateDesc,
            1,
            20);

        Assert.False(UnifiedSearchProviderPolicy.IsProviderScope(criteria));
    }

    [Fact]
    public void IsProviderScopeReturnsFalseWhenFiltersArePresent()
    {
        var criteria = new SearchCriteria(
            "friends",
            SearchContentType.All,
            Guid.NewGuid(),
            2020,
            7m,
            9m,
            SearchSortOption.Relevance,
            1,
            20);

        Assert.False(UnifiedSearchProviderPolicy.IsProviderScope(criteria));
    }

    [Fact]
    public void ShouldCacheProviderResultWhenProviderSucceeded()
    {
        var criteria = CreateCriteria("missing-title");
        var result = EmptyResult(criteria);

        Assert.True(UnifiedSearchProviderPolicy.ShouldCacheProviderResult(result, providerSucceeded: true));
    }

    [Fact]
    public void ShouldNotCacheProviderResultWhenProviderFailed()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 2, 1);

        Assert.False(UnifiedSearchProviderPolicy.ShouldCacheProviderResult(result, providerSucceeded: false));
    }

    [Fact]
    public void ShouldCacheDbResultWhenCatalogHasItems()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 2, 1);

        Assert.True(UnifiedSearchProviderPolicy.ShouldCacheDbResult(result));
    }

    [Fact]
    public void ShouldNotCacheDbResultWhenCatalogIsEmpty()
    {
        var criteria = CreateCriteria("missing-title");
        var result = EmptyResult(criteria);

        Assert.False(UnifiedSearchProviderPolicy.ShouldCacheDbResult(result));
    }

    private static SearchCriteria CreateCriteria(string query) =>
        new(query, SearchContentType.All, null, null, null, null, SearchSortOption.Relevance, 1, 20);

    private static PaginatedResult<SearchItem> EmptyResult(SearchCriteria criteria) =>
        new([], criteria.Page, criteria.PageSize, 0, 0);
}
