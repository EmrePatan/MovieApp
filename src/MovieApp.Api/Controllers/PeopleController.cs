using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.People;
using MovieApp.Contracts.People;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/people")]
public sealed class PeopleController(IGetPersonByTmdbIdService getPersonByTmdbIdService) : ControllerBase
{
    [HttpGet("tmdb/{tmdbPersonId:int}")]
    [ProducesResponseType(typeof(PersonDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonDetailResponse>> GetByTmdbId(
        int tmdbPersonId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getPersonByTmdbIdService.GetAsync(tmdbPersonId, cancellationToken);
            return Ok(PersonContractMapper.ToResponse(result));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Person not found.",
                exception.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid person request.",
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
