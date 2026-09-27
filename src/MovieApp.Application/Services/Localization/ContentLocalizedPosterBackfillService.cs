using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Localization;

public sealed class ContentLocalizedPosterBackfillService(
    IContentLocalizedPosterBackfillRepository backfillRepository,
    IServiceScopeFactory scopeFactory) : IContentLocalizedPosterBackfillService
{
    public async Task<ContentLocalizedPosterBackfillResult> RunAsync(
        ContentLocalizedPosterBackfillRequest request,
        CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Clamp(request.BatchSize, 1, 500);
        var maxItems = Math.Max(1, request.MaxItems);
        var maxConcurrency = Math.Max(1, request.MaxConcurrency);
        var delay = TimeSpan.FromMilliseconds(Math.Max(0, request.DelayBetweenRequestsMs));

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var providerCalls = 0;
        Guid? lastMovieId = request.StartAfterMovieId;
        Guid? lastTvId = request.StartAfterTvShowId;

        while (processed < maxItems)
        {
            var remaining = maxItems - processed;
            var candidates = await backfillRepository.SelectCandidatesAsync(
                Math.Min(batchSize, remaining),
                lastMovieId,
                lastTvId,
                cancellationToken);

            if (candidates.Count == 0)
            {
                break;
            }

            using var concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = candidates.Select(async candidate =>
            {
                await concurrencyLimiter.WaitAsync(cancellationToken);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var movieProvider = scope.ServiceProvider.GetRequiredService<IMovieDataProvider>();
                    var tvProvider = scope.ServiceProvider.GetRequiredService<ITvShowDataProvider>();
                    var synchronizer = scope.ServiceProvider.GetRequiredService<IContentLocalizedPosterSynchronizer>();

                    var externalId = $"tmdb-{candidate.TmdbId}";
                    if (candidate.ContentType == CatalogContentType.Movie)
                    {
                        var details = await movieProvider.GetMovieAsync(externalId, includeKeywords: false, cancellationToken);
                        Interlocked.Increment(ref providerCalls);
                        if (details is null)
                        {
                            Interlocked.Increment(ref failed);
                            return;
                        }

                        await synchronizer.SyncFromProviderPostersAsync(
                            CatalogContentType.Movie,
                            candidate.ContentId,
                            details.PosterPath,
                            details.ProviderPosters,
                            details.OriginalLanguage,
                            details.PrimaryOriginCountryCode,
                            cancellationToken);
                    }
                    else
                    {
                        var details = await tvProvider.GetTvShowAsync(externalId, includeKeywords: false, cancellationToken);
                        Interlocked.Increment(ref providerCalls);
                        if (details is null)
                        {
                            Interlocked.Increment(ref failed);
                            return;
                        }

                        await synchronizer.SyncFromProviderPostersAsync(
                            CatalogContentType.Tv,
                            candidate.ContentId,
                            details.PosterPath,
                            details.ProviderPosters,
                            details.OriginalLanguage,
                            details.PrimaryOriginCountryCode,
                            cancellationToken);
                    }

                    Interlocked.Increment(ref succeeded);
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    concurrencyLimiter.Release();
                }
            });

            await Task.WhenAll(tasks);
            processed += candidates.Count;

            foreach (var candidate in candidates)
            {
                if (candidate.ContentType == CatalogContentType.Movie)
                {
                    lastMovieId = candidate.ContentId;
                }
                else
                {
                    lastTvId = candidate.ContentId;
                }
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new ContentLocalizedPosterBackfillResult(
            processed,
            succeeded,
            failed,
            providerCalls,
            lastMovieId,
            lastTvId);
    }
}
