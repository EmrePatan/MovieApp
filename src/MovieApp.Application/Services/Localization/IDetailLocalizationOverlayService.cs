using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.Localization;

public interface IDetailLocalizationOverlayService
{
    Task<MovieDetailLocalizationData?> LoadMovieOverlayAsync(
        int? tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    MovieDetailsResult ApplyLoadedMovieOverlay(
        MovieDetailsResult canonical,
        MovieDetailLocalizationData? overlay);

    Task<TvShowDetailLocalizationData?> LoadTvShowOverlayAsync(
        int? tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    TvShowDetailsResult ApplyLoadedTvShowOverlay(
        TvShowDetailsResult canonical,
        TvShowDetailLocalizationData? overlay);

    Task<TvSeasonDetailLocalizationData?> LoadTvSeasonOverlayAsync(
        int? tmdbId,
        int seasonNumber,
        string contentLocale,
        CancellationToken cancellationToken = default);

    SeasonResult ApplyLoadedSeasonOverlay(
        SeasonResult canonical,
        TvSeasonDetailLocalizationData? overlay);

    EpisodeResult ApplyLoadedEpisodeOverlay(
        EpisodeResult canonical,
        TvSeasonDetailLocalizationData? overlay);

    Task<MovieDetailsResult> ApplyMovieOverlayAsync(
        MovieDetailsResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowDetailsResult> ApplyTvShowOverlayAsync(
        TvShowDetailsResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<SeasonResult> ApplySeasonOverlayAsync(
        SeasonResult canonical,
        int tvShowTmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<EpisodeResult> ApplyEpisodeOverlayAsync(
        EpisodeResult canonical,
        int tvShowTmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PersonDetailResult> ApplyPersonOverlayAsync(
        PersonDetailResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<CollectionDetailResult> ApplyCollectionOverlayAsync(
        CollectionDetailResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
