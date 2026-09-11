using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieByIdService(IMovieRepository movieRepository) : IGetMovieByIdService
{
    public async Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.GetByIdAsync(id, cancellationToken);
        if (movie is null)
        {
            throw new NotFoundException($"Movie with id '{id}' was not found.");
        }

        return MovieMapper.ToDetailsResult(movie);
    }
}
