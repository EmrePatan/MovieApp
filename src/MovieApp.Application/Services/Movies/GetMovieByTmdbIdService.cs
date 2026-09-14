using System.Globalization;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieByTmdbIdService(
    IMovieRepository movieRepository,
    IMovieDataProvider movieDataProvider,
    IGetMovieByIdService getMovieByIdService) : IGetMovieByTmdbIdService
{
    public async Task<MovieDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        if (tmdbId <= 0)
        {
            throw new ValidationException("A valid TMDB movie id is required.");
        }

        var movie = await movieRepository.GetByTmdbIdAsync(tmdbId, cancellationToken);
        if (movie is null)
        {
            var providerDetails = await movieDataProvider.GetMovieAsync(
                tmdbId.ToString(CultureInfo.InvariantCulture),
                cancellationToken);

            if (providerDetails is null)
            {
                throw new NotFoundException("The requested movie was not found.");
            }

            movie = await movieRepository.UpsertFromProviderAsync(providerDetails, cancellationToken);
        }

        return await getMovieByIdService.GetByIdAsync(movie.Id, cancellationToken);
    }
}
