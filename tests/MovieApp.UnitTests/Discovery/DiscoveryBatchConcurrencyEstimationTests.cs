using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class DiscoveryBatchConcurrencyEstimationTests
{
    [Fact]
    public void GenreCoverBatch_UsesLowerTopLevelConcurrencyForTypeAll()
    {
        var concurrency = DiscoveryBatchOrchestration.ResolveGenreCoverBatchConcurrency(
            SearchContentType.All,
            "tr-TR");

        Assert.Equal(DiscoveryBatchOrchestration.GenreCoverBatchMaxConcurrentOperations, concurrency);
    }

    [Fact]
    public void GenreCoverBatch_LocalizedTypeAll_HasBoundedLowLevelProviderBurst()
    {
        var topLevel = DiscoveryBatchOrchestration.ResolveGenreCoverBatchConcurrency(
            SearchContentType.All,
            "tr-TR");
        var lowLevel = DiscoveryBatchOrchestration.EstimateMaxConcurrentProviderCallsForGenreBatch(
            topLevel,
            SearchContentType.All,
            "tr-TR");

        Assert.Equal(2, topLevel);
        Assert.Equal(8, lowLevel);
        Assert.True(lowLevel <= 8);
    }

    [Fact]
    public void ProviderPreviewBatch_LocalizedMovie_HasBoundedLowLevelProviderBurst()
    {
        var topLevel = DiscoveryBatchOrchestration.ProviderPreviewBatchMaxConcurrentOperations;
        var lowLevel = DiscoveryBatchOrchestration.EstimateMaxConcurrentProviderCallsForProviderPreviewBatch(
            topLevel,
            "tr-TR");

        Assert.Equal(4, topLevel);
        Assert.Equal(8, lowLevel);
    }

    [Fact]
    public void GenreCoverBatch_EnglishLocale_HasLowerLocalizedMultiplier()
    {
        var lowLevel = DiscoveryBatchOrchestration.EstimateMaxConcurrentProviderCallsForGenreBatch(
            DiscoveryBatchOrchestration.GenreCoverBatchMaxConcurrentOperations,
            SearchContentType.All,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(4, lowLevel);
    }
}
