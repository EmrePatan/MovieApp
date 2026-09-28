using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Enums;
using MovieApp.UnitTests.Localization;

namespace MovieApp.UnitTests.TvShows;

public sealed class GetTvShowByIdLocalizedPosterTests
{
    private static readonly Guid TvShowId = Guid.Parse("72a0b57f-ff27-4ad2-a27e-1fc1f92f94aa");
    private const string CanonicalPoster = "/wA22c78uery60YBCle4tlcDBgsm.jpg";
    private const string TurkishPoster = "/hIIuHSyYpD3lSOj197ujHookMWI.jpg";

    [Fact]
    public async Task GetByIdAsync_WithTurkishLocale_ReplacesCanonicalPosterOnCacheHit()
    {
        var posterRepository = new InMemoryContentLocalizedPosterRepository();
        await posterRepository.UpsertAsync(
            CatalogContentType.Tv,
            TvShowId,
            SupportedArtworkLanguageKeys.Turkish,
            TurkishPoster,
            DateTime.UtcNow);

        var cache = new SeedTvShowDetailsCacheService(CreateCachedDetails());
        var service = new GetTvShowByIdService(
            new NoOpSeasonSummaryHydrator(),
            new NoOpCatalogSyncStateService(),
            cache,
            posterRepository,
            GetTvShowByIdServiceTestSupport.NoOpContentLocalizedPosterSynchronizer,
            GetTvShowByIdServiceTestSupport.NoOpTvShowDataProvider,
            GetTvShowByIdServiceTestSupport.NoOpExternalIdLookup,
            GetTvShowByIdServiceTestSupport.NoOpDetailLocalizationOverlay);

        var result = await service.GetByIdAsync(TvShowId, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(TurkishPoster, result.PosterPath);
    }

    [Fact]
    public async Task GetByIdAsync_SkipsProviderPosterFetch_ForNonTurkishShow()
    {
        var provider = new CountingTvShowDataProvider();
        var cache = new SeedTvShowDetailsCacheService(CreateCachedDetails() with
        {
            Title = "Interstellar",
            OriginalTitle = "Interstellar",
            OriginalLanguage = "en",
            PrimaryOriginCountryCode = "US"
        });
        var service = CreateService(cache, provider, new InMemoryContentLocalizedPosterRepository());

        var result = await service.GetByIdAsync(TvShowId, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(0, provider.GetTvShowCallCount);
        Assert.Equal(CanonicalPoster, result.PosterPath);
    }

    [Fact]
    public async Task GetByIdAsync_FetchesProviderPosterOnce_ForTurkishShowMissingArtwork()
    {
        var provider = new CountingTvShowDataProvider();
        var cache = new MemoryPosterCache(CreateCachedDetails());
        var service = CreateService(cache, provider, new InMemoryContentLocalizedPosterRepository());

        await service.GetByIdAsync(TvShowId, ContentLocaleResolver.TurkishTurkey);
        await service.GetByIdAsync(TvShowId, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, provider.GetTvShowCallCount);
    }

    private static GetTvShowByIdService CreateService(
        ICacheService cache,
        ITvShowDataProvider provider,
        IContentLocalizedPosterRepository posterRepository) =>
        new(
            new NoOpSeasonSummaryHydrator(),
            new NoOpCatalogSyncStateService(),
            cache,
            posterRepository,
            GetTvShowByIdServiceTestSupport.NoOpContentLocalizedPosterSynchronizer,
            provider,
            GetTvShowByIdServiceTestSupport.NoOpExternalIdLookup,
            GetTvShowByIdServiceTestSupport.NoOpDetailLocalizationOverlay);

    [Fact]
    public void DependencyInjection_ResolvesGetTvShowByIdService_WithPosterRepository()
    {
        var posterRepository = new CountingContentLocalizedPosterRepository();
        var services = new ServiceCollection();
        services.AddSingleton<IContentLocalizedPosterRepository>(posterRepository);
        services.AddSingleton<ITvShowSeasonSummaryHydrator, NoOpSeasonSummaryHydrator>();
        services.AddSingleton<ITvShowCatalogSyncStateService, NoOpCatalogSyncStateService>();
        services.AddSingleton<ICacheService, NoOpCacheService>();
        services.AddSingleton<ITvShowExternalIdLookup>(GetTvShowByIdServiceTestSupport.NoOpExternalIdLookup);
        services.AddSingleton<IDetailLocalizationOverlayService>(
            GetTvShowByIdServiceTestSupport.NoOpDetailLocalizationOverlay);
        services.AddSingleton(
            GetTvShowByIdServiceTestSupport.NoOpContentLocalizedPosterSynchronizer);
        services.AddSingleton(GetTvShowByIdServiceTestSupport.NoOpTvShowDataProvider);
        services.AddSingleton<IGetTvShowByIdService, GetTvShowByIdService>();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IGetTvShowByIdService>();

        Assert.IsType<GetTvShowByIdService>(service);
    }

    private static TvShowDetailsResult CreateCachedDetails() =>
        new(
            TvShowId,
            TmdbId: 111414,
            TvdbId: null,
            ImdbId: null,
            Title: "Alev Alev",
            OriginalTitle: "Alev Alev",
            Overview: "Overview",
            FirstAirDate: new DateOnly(2020, 11, 5),
            LastAirDate: new DateOnly(2021, 5, 27),
            PosterPath: CanonicalPoster,
            BackdropPath: null,
            OriginalLanguage: "tr",
            VoteAverage: 6.5m,
            VoteCount: 6,
            Status: "Canceled",
            Genres: ["Drama"],
            Seasons:
            [
                new SeasonSummaryResult(
                    Guid.NewGuid(),
                    1,
                    "1. Sezon",
                    new DateOnly(2020, 11, 5),
                    28,
                    null)
            ],
            CanFollow: false);

    private sealed class SeedTvShowDetailsCacheService(TvShowDetailsResult details) : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (typeof(T) == typeof(TvShowDetailsCacheEntry) &&
                key == TvShowDetailsCacheKeys.Create(TvShowId))
            {
                return Task.FromResult(new TvShowDetailsCacheEntry { Result = details } as T);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpSeasonSummaryHydrator : ITvShowSeasonSummaryHydrator
    {
        public Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Cache hit path should not hydrate.");
    }

    private sealed class NoOpCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public Task MarkRefreshedAsync(
            Guid tvShowId,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangeSignalAsync(
            Guid tvShowId,
            DateOnly changeSignalDate,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangesSyncAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkHotReleaseAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateTime? nextHotCheckAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateNextHotCheckAsync(
            Guid tvShowId,
            DateTime? nextHotCheckAtUtc,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CountingContentLocalizedPosterRepository : IContentLocalizedPosterRepository
    {
        public int GetPosterPathsCallCount { get; private set; }

        public Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> GetPosterPathsAsync(
            IReadOnlyList<ContentLocalizedPosterKey> keys,
            string languageKey,
            CancellationToken cancellationToken = default)
        {
            GetPosterPathsCallCount++;
            return Task.FromResult<IReadOnlyDictionary<ContentLocalizedPosterKey, string>>(
                new Dictionary<ContentLocalizedPosterKey, string>());
        }

        public Task UpsertAsync(
            CatalogContentType contentType,
            Guid contentId,
            string languageKey,
            string posterPath,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            CatalogContentType contentType,
            Guid contentId,
            string languageKey,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CountingTvShowDataProvider : ITvShowDataProvider
    {
        public int GetTvShowCallCount { get; private set; }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default)
        {
            GetTvShowCallCount++;
            return Task.FromResult<TvShowProviderDetails?>(null);
        }

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MemoryPosterCache(TvShowDetailsResult details) : ICacheService
    {
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (typeof(T) == typeof(TvShowDetailsCacheEntry) &&
                key == TvShowDetailsCacheKeys.Create(TvShowId))
            {
                return Task.FromResult(new TvShowDetailsCacheEntry { Result = details } as T);
            }

            if (_values.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
