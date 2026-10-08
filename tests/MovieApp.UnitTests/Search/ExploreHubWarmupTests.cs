using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Search;

public sealed class ExploreHubWarmupTests
{
    [Fact]
    public void MobileGenreRailRequestMatchesTheWarmedCacheKey()
    {
        var genreId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var warmed = GenreHubRailWarmup.CreateCriteria(genreId);

        Assert.True(DiscoverBrowseValidator.TryParseMode("popular", out var mode));
        Assert.True(AdvancedSearchValidator.TryParseType("all", out var type));
        Assert.True(DiscoverBrowseValidator.TryParseSort("popularity_desc", out var sort));

        var mobile = new DiscoverBrowseCriteria(
            mode,
            type,
            DiscoverBrowseValidator.ParseGenreIds([genreId.ToString()], null),
            Year: null,
            YearFrom: null,
            YearTo: null,
            MinRating: null,
            MinVoteCount: null,
            MinRuntimeMinutes: null,
            MaxRuntimeMinutes: null,
            Language: null,
            OriginCountry: null,
            AdvancedDiscoverValidator.ParseKeywordIds(null),
            AdvancedDiscoverValidator.ParseTvStatuses(null),
            sort,
            Page: 1,
            PageSize: 20);

        foreach (var locale in new[] { SupportedContentLocales.TurkishTurkey, SupportedContentLocales.EnglishUnitedStates })
        {
            Assert.Equal(
                DiscoveryBrowseCacheKeys.Create(mobile, locale, weeklyTrendingSnapshotGeneration: 0),
                DiscoveryBrowseCacheKeys.Create(warmed, locale, weeklyTrendingSnapshotGeneration: 0));
        }
    }

    [Fact]
    public async Task WarmAsync_LoadsExplorePreviewForBothLocalesAndTurkishWatchProviders()
    {
        var preview = new RecordingExplorePreviewService();
        var providers = new RecordingWatchProvidersService();
        var services = new ServiceCollection()
            .AddSingleton<IExplorePreviewService>(preview)
            .AddSingleton<IDiscoveryWatchProvidersService>(providers)
            .AddSingleton<IGenreReadRepository>(new NamedGenreRepository(new Dictionary<string, Guid>()))
            .AddSingleton<IDiscoverBrowseService>(new RecordingBrowseService())
            .BuildServiceProvider();

        await ExploreHubWarmup.WarmAsync(services, CancellationToken.None);

        Assert.Equal(
            [SupportedContentLocales.TurkishTurkey, SupportedContentLocales.EnglishUnitedStates],
            preview.Locales);
        Assert.All(preview.SectionSizes, size => Assert.Equal(ExploreHubWarmup.SectionSize, size));
        Assert.Equal([SearchContentType.Movie, SearchContentType.Tv], providers.MediaTypes);
        Assert.All(providers.Regions, region => Assert.Equal("TR", region));
    }

    [Fact]
    public async Task WarmAsync_BrowsEsEachResolvedGenreRailForBothLocales()
    {
        var ids = GenreHubRailWarmup.CanonicalGenreNames
            .Select((name, index) => (Name: name, Id: Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}")))
            .ToDictionary(item => item.Name, item => item.Id, StringComparer.Ordinal);
        var browse = new RecordingBrowseService();
        var services = new ServiceCollection()
            .AddSingleton<IExplorePreviewService>(new RecordingExplorePreviewService())
            .AddSingleton<IDiscoveryWatchProvidersService>(new RecordingWatchProvidersService())
            .AddSingleton<IGenreReadRepository>(new NamedGenreRepository(ids))
            .AddSingleton<IDiscoverBrowseService>(browse)
            .BuildServiceProvider();

        await ExploreHubWarmup.WarmAsync(services, CancellationToken.None);

        Assert.Equal(GenreHubRailWarmup.CanonicalGenreNames.Length * ExploreHubWarmup.ContentLocales.Length, browse.Calls.Count);
        foreach (var name in GenreHubRailWarmup.CanonicalGenreNames)
        {
            var expected = GenreHubRailWarmup.CreateCriteria(ids[name]);
            foreach (var locale in ExploreHubWarmup.ContentLocales)
            {
                var call = browse.Calls.Single(item =>
                    item.Locale == locale && item.Criteria.GenreIds.SequenceEqual(expected.GenreIds));
                Assert.Equal(
                    DiscoveryBrowseCacheKeys.Create(expected, locale),
                    DiscoveryBrowseCacheKeys.Create(call.Criteria, locale));
            }
        }
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

    private sealed class NamedGenreRepository(IReadOnlyDictionary<string, Guid> ids) : IGenreReadRepository
    {
        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(ids.TryGetValue(name, out var id) ? id : (Guid?)null);

        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
            IReadOnlyList<Guid> movieIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingBrowseService : IDiscoverBrowseService
    {
        public List<(DiscoverBrowseCriteria Criteria, string Locale)> Calls { get; } = [];

        public Task<PaginatedResult<SearchItem>> BrowseAsync(
            DiscoverBrowseCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((criteria, contentLocale));
            return Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
        }
    }
}
