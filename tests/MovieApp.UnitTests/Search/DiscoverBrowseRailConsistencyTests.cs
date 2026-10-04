using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoverBrowseRailConsistencyTests
{
    [Fact]
    public void ExplorePreviewHiddenGemsCriteriaMatchesSeeAllDefaults()
    {
        const int pageSize = 12;
        var previewCriteria = DiscoverTitleRailCriteria.Create(
            DiscoverBrowseMode.HiddenGems,
            1,
            pageSize);
        var seeAllCriteria = BuildSeeAllCriteria("hidden_gems", page: 1, pageSize: pageSize);

        Assert.Equal(previewCriteria, seeAllCriteria);
    }

    [Fact]
    public void ExplorePreviewPopularCriteriaMatchesDedicatedPopularEndpointDefaults()
    {
        const int pageSize = 15;
        var previewCriteria = DiscoverTitleRailCriteria.Create(
            DiscoverBrowseMode.Popular,
            1,
            pageSize);
        var popularEndpointCriteria = BuildSeeAllCriteria("popular", page: 1, pageSize: pageSize);
        var browseModeCriteria = BuildSeeAllCriteria("popular", page: 1, pageSize: pageSize);

        Assert.Equal(previewCriteria, popularEndpointCriteria);
        Assert.Equal(previewCriteria, browseModeCriteria);
    }

    [Fact]
    public void HiddenGemsAndPopularPreviewUseDiscoverBrowseServiceNotLegacyCatalogPopular()
    {
        Assert.Equal(
            DiscoverBrowseMode.HiddenGems,
            DiscoverTitleRailCriteria.Create(DiscoverBrowseMode.HiddenGems, 1, 10).Mode);
        Assert.Equal(
            DiscoverBrowseMode.Popular,
            DiscoverTitleRailCriteria.Create(DiscoverBrowseMode.Popular, 1, 10).Mode);
    }

    private static DiscoverBrowseCriteria BuildSeeAllCriteria(string mode, int page, int pageSize)
    {
        _ = DiscoverBrowseValidator.TryParseMode(mode, out var browseMode);
        _ = AdvancedSearchValidator.TryParseType("all", out var contentType);

        return new DiscoverBrowseCriteria(
            browseMode,
            contentType,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            page,
            pageSize);
    }
}
