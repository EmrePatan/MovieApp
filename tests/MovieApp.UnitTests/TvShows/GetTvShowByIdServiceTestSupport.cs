using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;
using MovieApp.UnitTests.Localization;

namespace MovieApp.UnitTests.TvShows;

internal static class GetTvShowByIdServiceTestSupport
{
    internal static readonly ITvShowExternalIdLookup NoOpExternalIdLookup =
        new NoOpTvShowExternalIdLookup();

    internal static readonly IDetailLocalizationOverlayService NoOpDetailLocalizationOverlay =
        new NoOpDetailLocalizationOverlayService();

    internal static readonly IContentLocalizedPosterSynchronizer NoOpContentLocalizedPosterSynchronizer =
        new NoOpContentLocalizedPosterSynchronizer();

    internal static readonly ITvShowDataProvider NoOpTvShowDataProvider =
        new StubTvShowDataProvider();

    private sealed class StubTvShowDataProvider : ITvShowDataProvider
    {
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

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

    private sealed class NoOpTvShowExternalIdLookup : ITvShowExternalIdLookup
    {
        public Task<TvShowExternalIds?> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowExternalIds?>(null);
    }

    private sealed class NoOpDetailLocalizationOverlayService : IDetailLocalizationOverlayService
    {
        public Task<MovieDetailLocalizationData?> LoadMovieOverlayAsync(
            int? tmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieDetailLocalizationData?>(null);

        public MovieDetailsResult ApplyLoadedMovieOverlay(
            MovieDetailsResult canonical,
            MovieDetailLocalizationData? overlay,
            string? contentLocale = null) => canonical;

        public Task<TvShowDetailLocalizationData?> LoadTvShowOverlayAsync(
            int? tmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowDetailLocalizationData?>(null);

        public TvShowDetailsResult ApplyLoadedTvShowOverlay(
            TvShowDetailsResult canonical,
            TvShowDetailLocalizationData? overlay,
            string? contentLocale = null) => canonical;

        public Task<TvSeasonDetailLocalizationData?> LoadTvSeasonOverlayAsync(
            int? tmdbId,
            int seasonNumber,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvSeasonDetailLocalizationData?>(null);

        public SeasonResult ApplyLoadedSeasonOverlay(
            SeasonResult canonical,
            TvSeasonDetailLocalizationData? overlay) => canonical;

        public EpisodeResult ApplyLoadedEpisodeOverlay(
            EpisodeResult canonical,
            TvSeasonDetailLocalizationData? overlay) => canonical;

        public Task<MovieDetailsResult> ApplyMovieOverlayAsync(
            MovieDetailsResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(canonical);

        public Task<TvShowDetailsResult> ApplyTvShowOverlayAsync(
            TvShowDetailsResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(canonical);

        public Task<SeasonResult> ApplySeasonOverlayAsync(
            SeasonResult canonical,
            int tvShowTmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(canonical);

        public Task<EpisodeResult> ApplyEpisodeOverlayAsync(
            EpisodeResult canonical,
            int tvShowTmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(canonical);

        public Task<PersonDetailResult> ApplyPersonOverlayAsync(
            PersonDetailResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(canonical);

        public Task<CollectionDetailResult> ApplyCollectionOverlayAsync(
            CollectionDetailResult canonical,
            string contentLocale,
            CancellationToken cancellationToken = default) => Task.FromResult(canonical);
    }
}
