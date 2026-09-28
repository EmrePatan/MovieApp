using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.TvShows;

namespace MovieApp.Application.Services.CatalogShare;

public sealed class CatalogPublicSharePageService(
    IGetMovieByIdService getMovieByIdService,
    IGetTvShowByIdService getTvShowByIdService,
    IOptions<CatalogShareOptions> catalogShareOptions) : ICatalogPublicSharePageService
{
    public async Task<CatalogPublicSharePageModel?> TryGetMoviePageAsync(
        Guid movieId,
        string? contentLocale,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var movie = string.IsNullOrWhiteSpace(contentLocale)
                ? await getMovieByIdService.GetByIdAsync(movieId, cancellationToken)
                : await getMovieByIdService.GetByIdAsync(movieId, contentLocale, cancellationToken);
            var options = catalogShareOptions.Value;
            var pageUrl = BuildCanonicalUrl(options, "movie", movieId);
            return new CatalogPublicSharePageModel(
                CatalogShareContentKind.Movie,
                movieId,
                movie.Title,
                movie.Overview,
                movie.ReleaseDate?.Year,
                movie.Genres,
                movie.PosterPath,
                pageUrl,
                pageUrl);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<CatalogPublicSharePageModel?> TryGetTvShowPageAsync(
        Guid tvShowId,
        string? contentLocale,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var show = string.IsNullOrWhiteSpace(contentLocale)
                ? await getTvShowByIdService.GetByIdAsync(tvShowId, cancellationToken)
                : await getTvShowByIdService.GetByIdAsync(tvShowId, contentLocale, cancellationToken);
            var options = catalogShareOptions.Value;
            var pageUrl = BuildCanonicalUrl(options, "tv", tvShowId);
            return new CatalogPublicSharePageModel(
                CatalogShareContentKind.TvShow,
                tvShowId,
                show.Title,
                show.Overview,
                show.FirstAirDate?.Year,
                show.Genres,
                show.PosterPath,
                pageUrl,
                pageUrl);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private static string BuildCanonicalUrl(CatalogShareOptions options, string segment, Guid id)
    {
        var baseUrl = options.PublicWebBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{segment}/{id:D}";
    }
}
