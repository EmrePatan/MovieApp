using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class DiscoveryBatchPerformanceCharacteristicsTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(23)]
    public async Task GenreCoverBatch_CountsOneBrowseSubOperationPerGenre(int genreCount)
    {
        var browse = new CountingDiscoverBrowseService();
        var service = CreateService(browse);
        var genreIds = Enumerable.Range(0, genreCount).Select(_ => Guid.NewGuid()).ToList();

        var stopwatch = Stopwatch.StartNew();
        await service.GetCandidatesAsync(
            new GenreCoverCandidatesBatchCriteria(genreIds, SearchContentType.All, 20),
            ContentLocaleResolver.EnglishUnitedStates);
        stopwatch.Stop();

        Assert.Equal(genreCount, browse.BrowseCallCount);
        Assert.True(
            browse.PeakConcurrentCalls <= DiscoveryBatchOrchestration.GenreCoverBatchMaxConcurrentOperations);
        Assert.True(stopwatch.ElapsedMilliseconds < 30_000);
    }

    [Fact]
    public async Task ProviderPreviewBatch_CountsOneAdvancedSubOperationPerProvider()
    {
        var advanced = new CountingAdvancedDiscoverService();
        var service = CreateProviderService(advanced);
        var providerIds = Enumerable.Range(1, 8).ToList();

        var stopwatch = Stopwatch.StartNew();
        await service.GetPreviewsAsync(
            new ProviderPreviewsBatchCriteria(providerIds, SearchContentType.Movie, "US", 1),
            ContentLocaleResolver.EnglishUnitedStates);
        stopwatch.Stop();

        Assert.Equal(8, advanced.DiscoverCallCount);
        Assert.True(advanced.PeakConcurrentCalls <= DiscoveryBatchOrchestration.ProviderPreviewBatchMaxConcurrentOperations);
        Assert.True(stopwatch.ElapsedMilliseconds < 30_000);
    }

    private static GenreCoverCandidatesBatchService CreateService(CountingDiscoverBrowseService browse)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDiscoverBrowseService>(browse);
        var provider = services.BuildServiceProvider();

        return new GenreCoverCandidatesBatchService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<GenreCoverCandidatesBatchService>.Instance);
    }

    private static ProviderPreviewsBatchService CreateProviderService(CountingAdvancedDiscoverService advanced)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAdvancedDiscoverService>(advanced);
        var provider = services.BuildServiceProvider();

        return new ProviderPreviewsBatchService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProviderPreviewsBatchService>.Instance);
    }

    private sealed class CountingDiscoverBrowseService : IDiscoverBrowseService
    {
        private int _active;
        public int BrowseCallCount { get; private set; }
        public int PeakConcurrentCalls { get; private set; }

        public async Task<PaginatedResult<SearchItem>> BrowseAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _active);
            PeakConcurrentCalls = Math.Max(PeakConcurrentCalls, _active);
            BrowseCallCount += 1;
            await Task.Delay(5, cancellationToken);
            Interlocked.Decrement(ref _active);

            return new PaginatedResult<SearchItem>([], 1, criteria.PageSize, 0, 0);
        }
    }

    private sealed class CountingAdvancedDiscoverService : IAdvancedDiscoverService
    {
        private int _active;
        public int DiscoverCallCount { get; private set; }
        public int PeakConcurrentCalls { get; private set; }

        public async Task<PaginatedResult<SearchItem>> DiscoverAsync(
            AdvancedDiscoverCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _active);
            PeakConcurrentCalls = Math.Max(PeakConcurrentCalls, _active);
            DiscoverCallCount += 1;
            await Task.Delay(5, cancellationToken);
            Interlocked.Decrement(ref _active);

            return new PaginatedResult<SearchItem>([], 1, criteria.PageSize, 0, 0);
        }
    }
}
