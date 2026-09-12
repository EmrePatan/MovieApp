using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class UnifiedSearchCatalogSatisfactionTests
{
    [Fact]
    public void ShouldUseProviderFallbackWhenCatalogIsEmpty()
    {
        var criteria = CreateCriteria("friends");
        var result = EmptyResult(criteria);

        Assert.True(UnifiedSearchCatalogSatisfaction.ShouldUseProviderFallback(criteria, result));
    }

    [Fact]
    public void ShouldUseProviderFallbackWhenFirstPageHasInsufficientResults()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 5, 1);

        Assert.True(UnifiedSearchCatalogSatisfaction.ShouldUseProviderFallback(criteria, result));
    }

    [Fact]
    public void ShouldNotUseProviderFallbackWhenCatalogCanFillRequestedPage()
    {
        var criteria = CreateCriteria("inception");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 20, 1);

        Assert.False(UnifiedSearchCatalogSatisfaction.ShouldUseProviderFallback(criteria, result));
    }

    [Fact]
    public void ShouldNotUseProviderFallbackWithoutQuery()
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
        var result = EmptyResult(criteria);

        Assert.False(UnifiedSearchCatalogSatisfaction.ShouldUseProviderFallback(criteria, result));
    }

    [Fact]
    public void ShouldNotCacheEmptyResults()
    {
        var criteria = CreateCriteria("missing");
        var result = EmptyResult(criteria);

        Assert.False(UnifiedSearchCatalogSatisfaction.ShouldCache(result));
    }

    [Fact]
    public void ShouldNotCachePartialCatalogResultsAfterProviderFailure()
    {
        var criteria = CreateCriteria("inception");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 5, 1);

        Assert.False(UnifiedSearchCatalogSatisfaction.ShouldCacheAfterSearch(
            result,
            criteria,
            providerIngestionSucceeded: false,
            providerIngestionFailed: true));
    }

    [Fact]
    public void ShouldCacheProviderRefreshedResultsEvenWhenPageIsNotFull()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 2, 1);

        Assert.True(UnifiedSearchCatalogSatisfaction.ShouldCacheAfterSearch(
            result,
            criteria,
            providerIngestionSucceeded: true,
            providerIngestionFailed: false));
    }

    private static SearchCriteria CreateCriteria(string query) =>
        new(query, SearchContentType.All, null, null, null, null, SearchSortOption.Relevance, 1, 20);

    private static PaginatedResult<SearchItem> EmptyResult(SearchCriteria criteria) =>
        new([], criteria.Page, criteria.PageSize, 0, 0);
}
