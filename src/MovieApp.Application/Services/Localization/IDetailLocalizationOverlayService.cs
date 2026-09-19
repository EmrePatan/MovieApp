using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.Localization;

public interface IDetailLocalizationOverlayService
{
    Task<MovieDetailsResult> ApplyMovieOverlayAsync(
        MovieDetailsResult canonical,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowDetailsResult> ApplyTvShowOverlayAsync(
        TvShowDetailsResult canonical,
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
