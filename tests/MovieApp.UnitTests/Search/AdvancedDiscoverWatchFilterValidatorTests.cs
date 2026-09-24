using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class AdvancedDiscoverWatchFilterValidatorTests
{
    [Fact]
    public void ValidateRejectsProvidersWithoutWatchRegion()
    {
        var criteria = new AdvancedDiscoverCriteria(
            SearchContentType.Movie,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
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
            null,
            [8],
            [WatchMonetizationType.Stream],
            AdvancedDiscoverSort.PopularityDesc,
            1,
            20);

        var validation = AdvancedDiscoverValidator.Validate(criteria);

        Assert.False(validation.IsValid);
        Assert.Contains("Watch region is required", validation.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseWatchProviderIdsSupportsRepeatedQueryValues()
    {
        var ids = AdvancedDiscoverValidator.ParseWatchProviderIds(["8", "337,119"]);

        Assert.Equal([8, 337, 119], ids);
    }

    [Fact]
    public void ParseWatchMonetizationTypesMapsStreamToEnum()
    {
        var types = AdvancedDiscoverValidator.ParseWatchMonetizationTypes(["stream", "rent"]);

        Assert.Equal([WatchMonetizationType.Stream, WatchMonetizationType.Rent], types);
    }
}
