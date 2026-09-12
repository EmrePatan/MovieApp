namespace MovieApp.Application.Services.Search;

internal static class ProviderDetailIngestionHelper
{
    internal static int ResolveDetailFetchLimit(int requestedPageSize, int configuredMaximum) =>
        Math.Min(Math.Max(1, requestedPageSize), Math.Max(1, configuredMaximum));

    internal static async Task IngestSummariesWithBoundedConcurrencyAsync<TSummary>(
        IReadOnlyList<TSummary> summaries,
        int detailFetchLimit,
        int maxConcurrentRequests,
        Func<TSummary, int, CancellationToken, Task> ingestSummaryAsync,
        CancellationToken cancellationToken)
    {
        if (summaries.Count == 0)
        {
            return;
        }

        var boundedConcurrency = Math.Max(1, maxConcurrentRequests);
        var fetchLimit = Math.Min(detailFetchLimit, summaries.Count);

        for (var index = 0; index < fetchLimit; index += boundedConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchSize = Math.Min(boundedConcurrency, fetchLimit - index);
            var batchTasks = new Task[batchSize];

            for (var batchIndex = 0; batchIndex < batchSize; batchIndex++)
            {
                var summaryIndex = index + batchIndex;
                var summary = summaries[summaryIndex];
                batchTasks[batchIndex] = ingestSummaryAsync(summary, summaryIndex, cancellationToken);
            }

            await Task.WhenAll(batchTasks);
        }
    }
}
