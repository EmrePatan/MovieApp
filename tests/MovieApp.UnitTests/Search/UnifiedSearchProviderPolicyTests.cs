using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using Microsoft.Extensions.Options;

namespace MovieApp.UnitTests.Search;

public sealed class UnifiedSearchProviderPolicyTests
{
    [Fact]
    public void NeedsProviderRefreshWhenCatalogIsEmpty()
    {
        var criteria = CreateCriteria("friends");
        var result = EmptyResult(criteria);

        Assert.True(UnifiedSearchProviderPolicy.NeedsProviderRefresh(
            criteria,
            result,
            lastRefreshedAtUtc: null,
            DateTime.UtcNow,
            TimeSpan.FromHours(24)));
    }

    [Fact]
    public void DoesNotNeedProviderRefreshWhenCatalogIsSufficientAndFresh()
    {
        var criteria = CreateCriteria("inception");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 20, 1);
        var lastRefreshed = DateTime.UtcNow.AddHours(-1);

        Assert.False(UnifiedSearchProviderPolicy.NeedsProviderRefresh(
            criteria,
            result,
            lastRefreshed,
            DateTime.UtcNow,
            TimeSpan.FromHours(24)));
    }

    [Fact]
    public void NeedsProviderRefreshWhenCatalogIsSufficientButStale()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 20, 1);
        var lastRefreshed = DateTime.UtcNow.AddHours(-25);

        Assert.True(UnifiedSearchProviderPolicy.NeedsProviderRefresh(
            criteria,
            result,
            lastRefreshed,
            DateTime.UtcNow,
            TimeSpan.FromHours(24)));
    }

    [Fact]
    public void DoesNotApplyProviderScopeWithoutQuery()
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

        Assert.False(UnifiedSearchProviderPolicy.IsProviderRefreshScope(criteria));
    }

    [Fact]
    public void DoesNotApplyProviderScopeForNonRelevanceSort()
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

        Assert.False(UnifiedSearchProviderPolicy.IsProviderRefreshScope(criteria));
    }

    [Fact]
    public void ShouldNotCacheFailedOrPartialRefresh()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 2, 1);

        Assert.False(UnifiedSearchProviderPolicy.ShouldCacheAfterSearch(
            result,
            criteria,
            providerRefreshFullySucceeded: false,
            providerRefreshFailedOrPartial: true));
    }

    [Fact]
    public void ShouldCacheSuccessfulProviderRefreshEvenWhenPageIsNotFull()
    {
        var criteria = CreateCriteria("friends");
        var result = new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 2, 1);

        Assert.True(UnifiedSearchProviderPolicy.ShouldCacheAfterSearch(
            result,
            criteria,
            providerRefreshFullySucceeded: true,
            providerRefreshFailedOrPartial: false));
    }

    private static SearchCriteria CreateCriteria(string query) =>
        new(query, SearchContentType.All, null, null, null, null, SearchSortOption.Relevance, 1, 20);

    private static PaginatedResult<SearchItem> EmptyResult(SearchCriteria criteria) =>
        new([], criteria.Page, criteria.PageSize, 0, 0);
}
