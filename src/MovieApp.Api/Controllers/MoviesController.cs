using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Movies;
using MovieApp.Contracts.Credits;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.Videos;
using MovieApp.Contracts.WatchProviders;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(
    ISearchMoviesService searchMoviesService,
    IGetMovieByIdService getMovieByIdService,
    IGetMovieByTmdbIdService getMovieByTmdbIdService,
    IGetMovieCreditsService getMovieCreditsService,
    IGetMovieWatchProvidersService getMovieWatchProvidersService,
    IGetMovieVideosService getMovieVideosService) : ControllerBase
{
    [HttpGet("search")]
    [EnableRateLimiting(SearchRateLimitPolicies.MovieSearch)]
    [ProducesResponseType(typeof(MovieSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MovieSearchResponse>> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new MovieSearchRequest(
                query ?? string.Empty,
                page ?? MovieSearchPagination.DefaultPage,
                pageSize ?? MovieSearchPagination.DefaultPageSize);

            var results = await searchMoviesService.SearchAsync(request, cancellationToken);
            return Ok(MovieContractMapper.ToSearchResponse(results));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid search request.",
                exception.Message));
        }
    }

    [HttpGet("tmdb/{tmdbId:int}")]
    [ProducesResponseType(typeof(MovieDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieDetailsResponse>> GetByTmdbId(
        int tmdbId,
        CancellationToken cancellationToken)
    {
        try
        {
            var movie = await getMovieByTmdbIdService.GetAsync(tmdbId, cancellationToken);
            return Ok(MovieContractMapper.ToDetailsResponse(movie));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid movie request.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MovieDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovieDetailsResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var movie = await getMovieByIdService.GetByIdAsync(id, cancellationToken);
            return Ok(MovieContractMapper.ToDetailsResponse(movie));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/credits")]
    [ProducesResponseType(typeof(CreditsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreditsResponse>> GetCredits(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var credits = await getMovieCreditsService.GetCreditsAsync(id, cancellationToken);
            return Ok(CreditsContractMapper.ToResponse(credits));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/videos")]
    [ProducesResponseType(typeof(VideosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VideosResponse>> GetVideos(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var videos = await getMovieVideosService.GetVideosAsync(id, cancellationToken);
            return Ok(VideosContractMapper.ToResponse(videos));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/watch-providers")]
    [ProducesResponseType(typeof(WatchProvidersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchProvidersResponse>> GetWatchProviders(
        Guid id,
        [FromQuery] string? region,
        CancellationToken cancellationToken)
    {
        try
        {
            var providers = await getMovieWatchProvidersService.GetWatchProvidersAsync(id, region, cancellationToken);
            return Ok(WatchProvidersContractMapper.ToResponse(providers));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid watch provider request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Movie not found.",
                exception.Message));
        }
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
