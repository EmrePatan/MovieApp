using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Movies;

public sealed class GetMovieByIdService(
    IMovieRepository movieRepository,
    IMovieRegionalReleaseRepository movieRegionalReleaseRepository,
    IOptions<ReleaseRegionOptions> releaseRegionOptions,
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

        var details = MovieMapper.ToDetailsResult(movie);
        var region = WatchProviderRegionValidator.Normalize(releaseRegionOptions.Value.DefaultRegion);
        var regionalRelease = await movieRegionalReleaseRepository.GetByMovieIdAndRegionAsync(
            movie.Id,
            region,
            cancellationToken);
        var effectiveReleaseDate = MovieFollowReleaseDateResolver.Resolve(regionalRelease, movie.ReleaseDate);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isReleased = MovieConsumptionReleaseGuardrail.IsReleasedForConsumption(effectiveReleaseDate, today);

        return details with { IsReleased = isReleased };
    }
}
