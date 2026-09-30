using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;
using MovieApp.Application.Recommendations;

namespace MovieApp.Infrastructure.Keywords;

public sealed class KeywordCatalogStatisticsRefreshService(
    IKeywordCatalogStatisticsLoader loader,
    KeywordCatalogStatisticsProvider provider,
    IOptions<KeywordCatalogStatisticsOptions> options,
    ILogger<KeywordCatalogStatisticsRefreshService> logger) : IKeywordCatalogStatisticsRefreshService
{
    public async Task<KeywordCatalogStatisticsRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            KeywordCatalogStatisticsLogMessages.LogRefreshSkippedDisabled(logger);
            return new KeywordCatalogStatisticsRefreshResult(
                Succeeded: true,
                SnapshotPublished: false,
                CatalogDocumentCount: provider.Current.CatalogDocumentCount,
                KeywordCount: provider.Current.KeywordCount,
                GenericKeywordCount: provider.Current.GenericKeywordCount,
                DurationMilliseconds: 0,
                FailureReason: "disabled");
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        KeywordCatalogStatisticsLogMessages.LogRefreshStarted(logger);

        try
        {
            var loadResult = await loader.LoadAsync(cancellationToken);
            stopwatch.Stop();

            if (loadResult is null)
            {
                KeywordCatalogStatisticsLogMessages.LogRefreshFailed(logger, "empty-load-result", stopwatch.ElapsedMilliseconds);
                return new KeywordCatalogStatisticsRefreshResult(
                    Succeeded: false,
                    SnapshotPublished: false,
                    CatalogDocumentCount: provider.Current.CatalogDocumentCount,
                    KeywordCount: provider.Current.KeywordCount,
                    GenericKeywordCount: provider.Current.GenericKeywordCount,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                    FailureReason: "empty-load-result");
            }

            var snapshot = KeywordCatalogStatisticsSnapshot.Create(
                loadResult,
                options.Value,
                DateTimeOffset.UtcNow);

            if (!snapshot.IsAvailable)
            {
                KeywordCatalogStatisticsLogMessages.LogRefreshFailed(logger, "invalid-snapshot", stopwatch.ElapsedMilliseconds);
                return new KeywordCatalogStatisticsRefreshResult(
                    Succeeded: false,
                    SnapshotPublished: false,
                    CatalogDocumentCount: provider.Current.CatalogDocumentCount,
                    KeywordCount: provider.Current.KeywordCount,
                    GenericKeywordCount: provider.Current.GenericKeywordCount,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                    FailureReason: "invalid-snapshot");
            }

            provider.Publish(snapshot);

            KeywordCatalogStatisticsLogMessages.LogRefreshCompleted(
                logger,
                snapshot.CatalogDocumentCount,
                snapshot.KeywordCount,
                snapshot.GenericKeywordCount,
                stopwatch.ElapsedMilliseconds,
                provider.IsFrequencyAwareActive);

            return new KeywordCatalogStatisticsRefreshResult(
                Succeeded: true,
                SnapshotPublished: true,
                CatalogDocumentCount: snapshot.CatalogDocumentCount,
                KeywordCount: snapshot.KeywordCount,
                GenericKeywordCount: snapshot.GenericKeywordCount,
                DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                FailureReason: null);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            KeywordCatalogStatisticsLogMessages.LogRefreshFailed(logger, exception.Message, stopwatch.ElapsedMilliseconds);
            return new KeywordCatalogStatisticsRefreshResult(
                Succeeded: false,
                SnapshotPublished: false,
                CatalogDocumentCount: provider.Current.CatalogDocumentCount,
                KeywordCount: provider.Current.KeywordCount,
                GenericKeywordCount: provider.Current.GenericKeywordCount,
                DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                FailureReason: exception.Message);
        }
    }
}
