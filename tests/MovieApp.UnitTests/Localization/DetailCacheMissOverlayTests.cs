using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.UnitTests.Keywords;
using MovieApp.UnitTests.Movies;
using MovieApp.UnitTests.TvShows;

namespace MovieApp.UnitTests.Localization;

public sealed class DetailCacheMissOverlayTests
{
    [Fact]
    public async Task MovieCacheMiss_AppliesLocalizedTitlesOnce()
    {
        var overlay = new CountingDetailLocalizationOverlay();
        var movie = new Movie
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            TmdbId = 496243,
            Title = "Parasite",
            OriginalTitle = "기생충",
            OriginalLanguage = "ko",
            PrimaryOriginCountryCode = "KR",
            ReleaseDate = new DateOnly(2019, 5, 30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var service = new GetMovieByIdService(
            new SingleMovieRepository(movie),
            new EmptyRegionalReleaseRepository(),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            new NoOpCatalogKeywordReadPathScheduler(),
            new NullMovieDataProvider(),
            new NoOpCatalogProviderUpsertService(),
            new MemoryCacheService(),
            new EmptyContentLocalizedPosterRepository(),
            overlay);

        var result = await service.GetByIdAsync(movie.Id, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, overlay.LoadCount);
        Assert.Equal(1, overlay.ApplyLoadedCount);
        Assert.Equal(0, overlay.ApplyAsyncCount);
        Assert.Equal("기생충", result.Title);
        Assert.Equal("Parazit", result.OriginalTitle);
    }

    [Fact]
    public async Task TvCacheMiss_AppliesLocalizedTitlesOnce()
    {
        var overlay = new CountingDetailLocalizationOverlay();
        var show = new TvShow
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            TmdbId = 1396,
            Title = "Breaking Bad",
            OriginalTitle = "Breaking Bad",
            OriginalLanguage = "en",
            PrimaryOriginCountryCode = "US",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var service = new GetTvShowByIdService(
            new FixedSeasonSummaryHydrator(show),
            new NoOpCatalogSyncStateService(),
            new MemoryCacheService(),
            new EmptyContentLocalizedPosterRepository(),
            GetTvShowByIdServiceTestSupport.NoOpContentLocalizedPosterSynchronizer,
            GetTvShowByIdServiceTestSupport.NoOpTvShowDataProvider,
            new FixedExternalIdLookup(show.TmdbId),
            overlay);

        var result = await service.GetByIdAsync(show.Id, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, overlay.LoadCount);
        Assert.Equal(1, overlay.ApplyLoadedCount);
        Assert.Equal(0, overlay.ApplyAsyncCount);
        Assert.Equal("Breaking Bad", result.Title);
        Assert.Equal("Breaking Bad: Yabancı", result.OriginalTitle);
    }

    private sealed class CountingDetailLocalizationOverlay : IDetailLocalizationOverlayService
    {
        private static readonly MovieDetailLocalizationData MovieOverlay = new("Parazit", "Özet", null);
        private static readonly TvShowDetailLocalizationData TvOverlay = new(
            "Breaking Bad: Yabancı",
            "Özet",
            "Ended",
            new Dictionary<int, string?>());

        public int LoadCount { get; private set; }

        public int ApplyLoadedCount { get; private set; }

        public int ApplyAsyncCount { get; private set; }

        public Task<MovieDetailLocalizationData?> LoadMovieOverlayAsync(
            int? tmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult<MovieDetailLocalizationData?>(MovieOverlay);
        }

        public MovieDetailsResult ApplyLoadedMovieOverlay(
            MovieDetailsResult canonical,
            MovieDetailLocalizationData? overlay,
            string? contentLocale = null)
        {
            ApplyLoadedCount++;
            var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                canonical.Title,
                canonical.OriginalTitle,
                canonical.OriginalLanguage,
                overlay?.Title,
                contentLocale ?? ContentLocaleResolver.TurkishTurkey,
                canonical.PrimaryOriginCountryCode);
            return canonical with
            {
                Title = titles.Title,
                OriginalTitle = titles.OriginalTitle,
                Overview = overlay?.Overview ?? canonical.Overview
            };
        }

        public Task<TvShowDetailLocalizationData?> LoadTvShowOverlayAsync(
            int? tmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult<TvShowDetailLocalizationData?>(TvOverlay);
        }

        public TvShowDetailsResult ApplyLoadedTvShowOverlay(
            TvShowDetailsResult canonical,
            TvShowDetailLocalizationData? overlay,
            string? contentLocale = null)
        {
            ApplyLoadedCount++;
            var titles = LocalizedDisplayTitleSelector.ChooseDisplayTitles(
                canonical.Title,
                canonical.OriginalTitle,
                canonical.OriginalLanguage,
                overlay?.Title,
                contentLocale ?? ContentLocaleResolver.TurkishTurkey,
                canonical.PrimaryOriginCountryCode);
            return canonical with
            {
                Title = titles.Title,
                OriginalTitle = titles.OriginalTitle,
                Overview = overlay?.Overview ?? canonical.Overview
            };
        }

        public Task<MovieDetailsResult> ApplyMovieOverlayAsync(
            MovieDetailsResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            ApplyAsyncCount++;
            return Task.FromResult(ApplyLoadedMovieOverlay(canonical, MovieOverlay, contentLocale));
        }

        public Task<TvShowDetailsResult> ApplyTvShowOverlayAsync(
            TvShowDetailsResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            ApplyAsyncCount++;
            return Task.FromResult(ApplyLoadedTvShowOverlay(canonical, TvOverlay, contentLocale));
        }

        public Task<TvSeasonDetailLocalizationData?> LoadTvSeasonOverlayAsync(
            int? tmdbId,
            int seasonNumber,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public SeasonResult ApplyLoadedSeasonOverlay(
            SeasonResult canonical,
            TvSeasonDetailLocalizationData? overlay) =>
            throw new NotSupportedException();

        public EpisodeResult ApplyLoadedEpisodeOverlay(
            EpisodeResult canonical,
            TvSeasonDetailLocalizationData? overlay) =>
            throw new NotSupportedException();

        public Task<SeasonResult> ApplySeasonOverlayAsync(
            SeasonResult canonical,
            int tvShowTmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeResult> ApplyEpisodeOverlayAsync(
            EpisodeResult canonical,
            int tvShowTmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonDetailResult> ApplyPersonOverlayAsync(
            PersonDetailResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CollectionDetailResult> ApplyCollectionOverlayAsync(
            CollectionDetailResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SingleMovieRepository(Movie movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(movie.Id == id ? movie : null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyRegionalReleaseRepository : IMovieRegionalReleaseRepository
    {
        public Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
            Guid movieId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieRegionalRelease?>(null);

        public Task<MovieRegionalRelease> UpsertAsync(
            MovieRegionalRelease regionalRelease,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(regionalRelease);
    }

    private sealed class MemoryCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedSeasonSummaryHydrator(TvShow show) : ITvShowSeasonSummaryHydrator
    {
        public Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowSeasonSummaryHydrationResult(show, false));
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

    private sealed class FixedExternalIdLookup(int? tmdbId) : ITvShowExternalIdLookup
    {
        public Task<TvShowExternalIds?> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowExternalIds?>(new TvShowExternalIds(tmdbId, null, null));
    }
}
