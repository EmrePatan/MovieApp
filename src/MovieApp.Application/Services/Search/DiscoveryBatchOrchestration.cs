using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

public static class DiscoveryBatchOrchestration
{
    /// <summary>
    /// Default cap for movie/tv-only batch sub-operations.
    /// </summary>
    internal const int DefaultMaxConcurrentOperations = 4;

    /// <summary>
    /// Genre cover batches use browse type=all, which fans out to multiple provider calls per genre.
    /// </summary>
    internal const int GenreCoverBatchMaxConcurrentOperations = 2;

    internal const int ProviderPreviewBatchMaxConcurrentOperations = 4;

    /// <summary>
    /// Worst-case localized browse type=all issues up to four provider discover calls per genre page.
    /// </summary>
    internal const int LocalizedBrowseAllProviderCallsPerGenre = 4;

    /// <summary>
    /// Worst-case localized advanced discover (movie) issues two provider calls per preview.
    /// </summary>
    internal const int LocalizedAdvancedDiscoverProviderCallsPerPreview = 2;
    public const int MaxGenreCoverBatchSize = 32;
    public const int MaxProviderPreviewBatchSize = 64;
    internal const int MinGenreCoverCandidatesPerGenre = 1;
    internal const int MaxGenreCoverCandidatesPerGenre = 20;
    internal const int MinProviderPreviewPageSize = 1;
    internal const int MaxProviderPreviewPageSize = 20;
    internal const int DefaultProviderPreviewPageSize = 1;

    internal static int ResolveGenreCoverBatchConcurrency(SearchContentType mediaType, string contentLocale)
    {
        if (mediaType == SearchContentType.All &&
            ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return GenreCoverBatchMaxConcurrentOperations;
        }

        return mediaType == SearchContentType.All
            ? GenreCoverBatchMaxConcurrentOperations
            : DefaultMaxConcurrentOperations;
    }

    internal static int EstimateMaxConcurrentProviderCallsForGenreBatch(
        int batchConcurrency,
        SearchContentType mediaType,
        string contentLocale)
    {
        var perGenre = mediaType == SearchContentType.All
            ? ContentLocaleResolver.RequiresLocalization(contentLocale)
                ? LocalizedBrowseAllProviderCallsPerGenre
                : 2
            : ContentLocaleResolver.RequiresLocalization(contentLocale)
                ? 2
                : 1;

        return batchConcurrency * perGenre;
    }

    internal static int EstimateMaxConcurrentProviderCallsForProviderPreviewBatch(
        int batchConcurrency,
        string contentLocale) =>
        batchConcurrency * (ContentLocaleResolver.RequiresLocalization(contentLocale)
            ? LocalizedAdvancedDiscoverProviderCallsPerPreview
            : 1);

    internal static async Task<IReadOnlyList<TResult>> ExecuteInOrderAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        int maxConcurrency,
        Func<TItem, CancellationToken, Task<TResult>> executeAsync,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var boundedConcurrency = Math.Max(1, maxConcurrency);
        var results = new TResult[items.Count];

        for (var batchStart = 0; batchStart < items.Count; batchStart += boundedConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchLength = Math.Min(boundedConcurrency, items.Count - batchStart);
            var batchTasks = new Task[batchLength];

            for (var batchOffset = 0; batchOffset < batchLength; batchOffset++)
            {
                var itemIndex = batchStart + batchOffset;
                batchTasks[batchOffset] = ExecuteItemAsync(itemIndex, executeAsync, items, results, cancellationToken);
            }

            await Task.WhenAll(batchTasks);
        }

        return results;
    }

    private static async Task ExecuteItemAsync<TItem, TResult>(
        int itemIndex,
        Func<TItem, CancellationToken, Task<TResult>> executeAsync,
        IReadOnlyList<TItem> items,
        TResult[] results,
        CancellationToken cancellationToken)
    {
        results[itemIndex] = await executeAsync(items[itemIndex], cancellationToken);
    }
}
