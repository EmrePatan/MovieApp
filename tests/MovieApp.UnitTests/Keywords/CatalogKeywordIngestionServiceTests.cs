using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.UnitTests.Keywords;

public sealed class CatalogKeywordIngestionServiceTests
{
    [Fact]
    public async Task TryEnrichMovieKeywordsAsyncSkipsWhenAlreadySyncedAndNotRefreshing()
    {
        var provider = new TrackingKeywordsProvider();
        var repository = new FakeKeywordCatalogRepository
        {
            MovieTarget = new KeywordEnrichmentTarget(27205, DateTime.UtcNow)
        };
        var service = CreateService(provider, repository);

        await service.TryEnrichMovieKeywordsAsync(Guid.NewGuid(), refreshKeywords: false, cancellationToken: CancellationToken.None);

        Assert.Equal(0, provider.MovieCalls);
        Assert.Equal(0, repository.MovieSyncCalls);
    }

    [Fact]
    public async Task TryEnrichMovieKeywordsAsyncFetchesWhenNeverSynced()
    {
        var movieId = Guid.NewGuid();
        var provider = new TrackingKeywordsProvider();
        var repository = new FakeKeywordCatalogRepository
        {
            MovieTarget = new KeywordEnrichmentTarget(27205, null)
        };
        var service = CreateService(provider, repository);

        await service.TryEnrichMovieKeywordsAsync(movieId, refreshKeywords: false, cancellationToken: CancellationToken.None);

        Assert.Equal(1, provider.MovieCalls);
        Assert.Equal(1, repository.MovieSyncCalls);
    }

    [Fact]
    public async Task TryEnrichMovieKeywordsAsyncPreservesStateOnProviderFailure()
    {
        var movieId = Guid.NewGuid();
        var provider = new TrackingKeywordsProvider { ThrowOnMovie = true };
        var repository = new FakeKeywordCatalogRepository
        {
            MovieTarget = new KeywordEnrichmentTarget(27205, null)
        };
        var service = CreateService(provider, repository);

        await service.TryEnrichMovieKeywordsAsync(movieId, refreshKeywords: false, cancellationToken: CancellationToken.None);

        Assert.Equal(1, provider.MovieCalls);
        Assert.Equal(0, repository.MovieSyncCalls);
    }

    [Fact]
    public async Task TryEnrichMovieKeywordsAsyncPropagatesCancellation()
    {
        var provider = new TrackingKeywordsProvider();
        var repository = new FakeKeywordCatalogRepository
        {
            MovieTarget = new KeywordEnrichmentTarget(27205, null)
        };
        var service = CreateService(provider, repository);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.TryEnrichMovieKeywordsAsync(Guid.NewGuid(), refreshKeywords: true, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task TryEnrichTvShowKeywordsAsyncFetchesWhenRefreshRequested()
    {
        var tvShowId = Guid.NewGuid();
        var provider = new TrackingKeywordsProvider();
        var repository = new FakeKeywordCatalogRepository
        {
            TvShowTarget = new KeywordEnrichmentTarget(1396, DateTime.UtcNow)
        };
        var service = CreateService(provider, repository);

        await service.TryEnrichTvShowKeywordsAsync(tvShowId, refreshKeywords: true, cancellationToken: CancellationToken.None);

        Assert.Equal(1, provider.TvCalls);
        Assert.Equal(1, repository.TvSyncCalls);
    }

    private static CatalogKeywordIngestionService CreateService(
        IKeywordsProvider provider,
        IKeywordCatalogRepository repository) =>
        new(provider, repository, NullLogger<CatalogKeywordIngestionService>.Instance);

    private sealed class TrackingKeywordsProvider : IKeywordsProvider
    {
        public int MovieCalls { get; private set; }

        public int TvCalls { get; private set; }

        public bool ThrowOnMovie { get; init; }

        public Task<IReadOnlyList<ProviderKeywordSummary>> GetMovieKeywordsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            cancellationToken.ThrowIfCancellationRequested();

            if (ThrowOnMovie)
            {
                throw new InvalidOperationException("provider failure");
            }

            return Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>(
                [new ProviderKeywordSummary(42, "time travel")]);
        }

        public Task<IReadOnlyList<ProviderKeywordSummary>> GetTvShowKeywordsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            TvCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>(
                [new ProviderKeywordSummary(42, "time travel")]);
        }
    }

    private sealed class FakeKeywordCatalogRepository : IKeywordCatalogRepository
    {
        public KeywordEnrichmentTarget? MovieTarget { get; init; }

        public KeywordEnrichmentTarget? TvShowTarget { get; init; }

        public int MovieSyncCalls { get; private set; }

        public int TvSyncCalls { get; private set; }

        public Task<KeywordEnrichmentTarget?> GetMovieKeywordTargetAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MovieTarget);

        public Task<KeywordEnrichmentTarget?> GetTvShowKeywordTargetAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(TvShowTarget);

        public Task SyncMovieKeywordsAsync(
            Guid movieId,
            IReadOnlyList<ProviderKeywordSummary> keywords,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MovieSyncCalls++;
            return Task.CompletedTask;
        }

        public Task SyncTvShowKeywordsAsync(
            Guid tvShowId,
            IReadOnlyList<ProviderKeywordSummary> keywords,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            TvSyncCalls++;
            return Task.CompletedTask;
        }
    }
}
