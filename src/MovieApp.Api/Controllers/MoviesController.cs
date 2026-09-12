using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Movies;
using MovieApp.Contracts.Movies;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController(
    ISearchMoviesService searchMoviesService,
    IGetMovieByIdService getMovieByIdService) : ControllerBase
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

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
