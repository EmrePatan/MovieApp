using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class ProviderPreviewsBatchServiceTests
{
    [Fact]
    public async Task GetPreviewsAsync_UsesStreamMonetizationAndWatchRegion()
    {
        var advanced = new FakeAdvancedDiscoverService();
        var service = CreateService(advanced);

        await service.GetPreviewsAsync(
            new ProviderPreviewsBatchCriteria([8], SearchContentType.Movie, "TR", 1),
            ContentLocaleResolver.EnglishUnitedStates);

        var captured = advanced.LastCriteria;
        Assert.NotNull(captured);
        Assert.Equal("TR", captured.WatchRegion);
        Assert.Equal([8], captured.WatchProviderIds);
        Assert.Equal([WatchMonetizationType.Stream], captured.WatchMonetizationTypes);
        Assert.Equal(AdvancedDiscoverSort.PopularityDesc, captured.Sort);
    }

    [Fact]
    public async Task GetPreviewsAsync_PropagatesUnexpectedExceptions()
    {
        var advanced = new FakeAdvancedDiscoverService
        {
            FaultingProviderIds = { 337 },
        };
        var service = CreateService(advanced);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetPreviewsAsync(
                new ProviderPreviewsBatchCriteria([337], SearchContentType.Movie, "US", 1),
                ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task GetPreviewsAsync_PartialFailureMarksTransientErrorStatus()
    {
        var advanced = new FakeAdvancedDiscoverService
        {
            UnavailableProviderIds = { 119 },
            Results = { [8] = [CreateItem("netflix-hit")] },
        };
        var service = CreateService(advanced);

        var groups = await service.GetPreviewsAsync(
            new ProviderPreviewsBatchCriteria([8, 119], SearchContentType.Movie, "US", 1),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(DiscoveryBatchItemStatus.Ok, groups[0].Status);
        Assert.Equal("netflix-hit", groups[0].Items[0].Title);
        Assert.Equal(DiscoveryBatchItemStatus.TransientError, groups[1].Status);
        Assert.Empty(groups[1].Items);
    }

    private static ProviderPreviewsBatchService CreateService(FakeAdvancedDiscoverService advanced)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAdvancedDiscoverService>(advanced);
        var provider = services.BuildServiceProvider();

        return new ProviderPreviewsBatchService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ProviderPreviewsBatchService>.Instance);
    }

    private static SearchItem CreateItem(string title) =>
        new(
            Guid.NewGuid(),
            "movie",
            title,
            title,
            null,
            null,
            null,
            null,
            0,
            0,
            2024,
            1);

    private sealed class FakeAdvancedDiscoverService : IAdvancedDiscoverService
    {
        public AdvancedDiscoverCriteria? LastCriteria { get; private set; }

        public Dictionary<int, List<SearchItem>> Results { get; } = new();

        public HashSet<int> UnavailableProviderIds { get; } = [];

        public HashSet<int> FaultingProviderIds { get; } = [];

        public Task<PaginatedResult<SearchItem>> DiscoverAsync(
            AdvancedDiscoverCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            LastCriteria = criteria;
            var providerId = criteria.WatchProviderIds.Single();
            if (FaultingProviderIds.Contains(providerId))
            {
                throw new InvalidOperationException("Simulated programming error.");
            }

            if (UnavailableProviderIds.Contains(providerId))
            {
                throw new SearchProviderUnavailableException();
            }

            var items = Results.TryGetValue(providerId, out var value) ? value : [];
            return Task.FromResult(new PaginatedResult<SearchItem>(items, 1, criteria.PageSize, items.Count, 1));
        }
    }
}
