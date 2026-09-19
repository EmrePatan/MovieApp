using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.People;
using MovieApp.Contracts.Images;
using MovieApp.Contracts.People;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/people")]
public sealed class PeopleController(
    IGetPersonByTmdbIdService getPersonByTmdbIdService,
    IGetPersonImagesService getPersonImagesService,
    IDetailLocalizationOverlayService detailLocalizationOverlayService) : ControllerBase
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
            result = await detailLocalizationOverlayService.ApplyPersonOverlayAsync(
                result,
                Request.ResolveContentLocale(),
                cancellationToken);
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

    [HttpGet("tmdb/{tmdbPersonId:int}/images")]
    [ProducesResponseType(typeof(ImagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImagesResponse>> GetImages(
        int tmdbPersonId,
        CancellationToken cancellationToken)
    {
        try
        {
            var images = await getPersonImagesService.GetImagesAsync(tmdbPersonId, cancellationToken);
            return Ok(ImagesContractMapper.ToResponse(images));
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
