using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.TvShows;

internal static class GetTvShowByIdServiceTestSupport
{
    internal static readonly ITvShowExternalIdLookup NoOpExternalIdLookup =
        new NoOpTvShowExternalIdLookup();

    internal static readonly IDetailLocalizationOverlayService NoOpDetailLocalizationOverlay =
        new NoOpDetailLocalizationOverlayService();

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
