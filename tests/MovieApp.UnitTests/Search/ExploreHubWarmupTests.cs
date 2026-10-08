using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class ExploreHubWarmupTests
{
    [Fact]
    public async Task WarmAsync_LoadsExplorePreviewForBothLocalesAndTurkishWatchProviders()
    {
        var preview = new RecordingExplorePreviewService();
        var providers = new RecordingWatchProvidersService();
        var services = new ServiceCollection()
            .AddSingleton<IExplorePreviewService>(preview)
            .AddSingleton<IDiscoveryWatchProvidersService>(providers)
            .BuildServiceProvider();

        await ExploreHubWarmup.WarmAsync(services, CancellationToken.None);

        Assert.Equal(
            [SupportedContentLocales.TurkishTurkey, SupportedContentLocales.EnglishUnitedStates],
            preview.Locales);
        Assert.All(preview.SectionSizes, size => Assert.Equal(ExploreHubWarmup.SectionSize, size));
        Assert.Equal([SearchContentType.Movie, SearchContentType.Tv], providers.MediaTypes);
        Assert.All(providers.Regions, region => Assert.Equal("TR", region));
    }

    private sealed class RecordingExplorePreviewService : IExplorePreviewService
    {
        public List<string> Locales { get; } = [];

        public List<int> SectionSizes { get; } = [];

        public Task<ExplorePreviewResult> GetPreviewAsync(
            ExplorePreviewCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            Locales.Add(contentLocale);
            SectionSizes.Add(criteria.SectionSize);
            return Task.FromResult(new ExplorePreviewResult(
                [],
                new PaginatedResult<SearchItem>([], 1, criteria.SectionSize, 0, 0),
                new PaginatedResult<SearchItem>([], 1, criteria.SectionSize, 0, 0),
                new PaginatedResult<SearchItem>([], 1, criteria.SectionSize, 0, 0),
                new PaginatedResult<SearchItem>([], 1, criteria.SectionSize, 0, 0)));
        }
    }

    private sealed class RecordingWatchProvidersService : IDiscoveryWatchProvidersService
    {
        public List<SearchContentType> MediaTypes { get; } = [];

        public List<string> Regions { get; } = [];

        public Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
            SearchContentType mediaType,
            string watchRegion,
            CancellationToken cancellationToken = default)
        {
            MediaTypes.Add(mediaType);
            Regions.Add(watchRegion);
            return Task.FromResult<IReadOnlyList<DiscoveryWatchProviderItem>>([]);
        }

        public Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
            SearchContentType mediaType,
            string watchRegion,
            bool includeTransactionalProviders,
            CancellationToken cancellationToken = default) =>
            GetWatchProvidersAsync(mediaType, watchRegion, cancellationToken);
    }
}
