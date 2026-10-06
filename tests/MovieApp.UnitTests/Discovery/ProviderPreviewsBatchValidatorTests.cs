using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Discovery;

public sealed class ProviderPreviewsBatchValidatorTests
{
    [Fact]
    public void CreateCriteria_DeduplicatesProviderIdsWhilePreservingOrder()
    {
        var criteria = ProviderPreviewsBatchValidator.CreateCriteria(
            [8, 119, 8],
            "movie",
            "US",
            1);

        Assert.Equal([8, 119], criteria.ProviderIds);
        Assert.Equal(SearchContentType.Movie, criteria.MediaType);
        Assert.Equal("US", criteria.WatchRegion);
    }

    [Fact]
    public void ValidateRequest_RejectsAllMediaType()
    {
        var validation = ProviderPreviewsBatchValidator.ValidateRequest([8], "all", "US", 1);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRequest_RejectsExcessiveBatchSize()
    {
        var ids = Enumerable.Range(1, DiscoveryBatchOrchestration.MaxProviderPreviewBatchSize + 1).ToList();
        var validation = ProviderPreviewsBatchValidator.ValidateRequest(ids, "movie", "US", 1);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void CreateCriteria_ThrowsWhenWatchRegionMissing()
    {
        Assert.Throws<ValidationException>(() =>
            ProviderPreviewsBatchValidator.CreateCriteria([8], "movie", "  ", 1));
    }
}
