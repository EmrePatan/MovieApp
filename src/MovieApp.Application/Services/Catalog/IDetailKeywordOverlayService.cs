using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.Catalog;

public interface IDetailKeywordOverlayService
{
    Task<MovieDetailsResult> ApplyMovieKeywordsAsync(
        Guid movieId,
        MovieDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowDetailsResult> ApplyTvShowKeywordsAsync(
        Guid tvShowId,
        TvShowDetailsResult result,
        string? contentLocale,
        CancellationToken cancellationToken = default);
}
