using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieByIdService(
    IMovieRepository movieRepository,
    ICatalogKeywordIngestionService catalogKeywordIngestionService) : IGetMovieByIdService
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

        await catalogKeywordIngestionService.TryEnrichMovieKeywordsAsync(
            movie.Id,
            refreshKeywords: false,
            cancellationToken);

        return MovieMapper.ToDetailsResult(movie);
    }
}
