using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Collections;
using MovieApp.Contracts.Collections;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/collections")]
public sealed class CollectionsController(IGetCollectionService getCollectionService) : ControllerBase
{
    [HttpGet("{tmdbCollectionId:int}")]
    [ProducesResponseType(typeof(CollectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CollectionResponse>> GetByTmdbId(
        int tmdbCollectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getCollectionService.GetAsync(tmdbCollectionId, cancellationToken);
            return Ok(CollectionContractMapper.ToResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid collection request.",
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
