using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ProviderDetailIngestionHelperTests
{
    [Theory]
    [InlineData(100, 20, 20)]
    [InlineData(10, 20, 10)]
    [InlineData(0, 20, 1)]
    public void ResolveDetailFetchLimitCapsRequestedPageSize(int pageSize, int configuredMax, int expected)
    {
        var actual = ProviderDetailIngestionHelper.ResolveDetailFetchLimit(pageSize, configuredMax);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task IngestSummariesWithBoundedConcurrencyAsyncNeverExceedsConfiguredDetailFetchLimit()
    {
        var processedIndexes = new List<int>();

        await ProviderDetailIngestionHelper.IngestSummariesWithBoundedConcurrencyAsync(
            Enumerable.Range(1, 100).Select(index => index).ToList(),
            detailFetchLimit: 15,
            maxConcurrentRequests: 4,
            async (_, index, cancellationToken) =>
            {
                processedIndexes.Add(index);
                await Task.Delay(1, cancellationToken);
            },
            CancellationToken.None);

        Assert.Equal(15, processedIndexes.Count);
        Assert.Equal(Enumerable.Range(0, 15), processedIndexes.OrderBy(index => index));
    }

    [Fact]
    public async Task IngestSummariesWithBoundedConcurrencyAsyncUsesBoundedBatchConcurrency()
    {
        var maxObservedConcurrency = 0;
        var currentConcurrency = 0;
        var sync = new object();

        await ProviderDetailIngestionHelper.IngestSummariesWithBoundedConcurrencyAsync(
            Enumerable.Range(1, 12).Select(index => index).ToList(),
            detailFetchLimit: 12,
            maxConcurrentRequests: 3,
            async (_, _, cancellationToken) =>
            {
                lock (sync)
                {
                    currentConcurrency++;
                    maxObservedConcurrency = Math.Max(maxObservedConcurrency, currentConcurrency);
                }

                await Task.Delay(25, cancellationToken);

                lock (sync)
                {
                    currentConcurrency--;
                }
            },
            CancellationToken.None);

        Assert.Equal(3, maxObservedConcurrency);
    }
}
