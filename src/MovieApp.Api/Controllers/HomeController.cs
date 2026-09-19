using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Home;
using Microsoft.Extensions.Options;

namespace MovieApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/home")]
public sealed class HomeController(IHomeService homeService, IOptions<HomeOptions> options) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HomeResponse>> GetHome(
        [FromQuery] string? type,
        [FromQuery] int? sectionSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = TryCreateCriteria(type, sectionSize, out var criteriaError);
            if (criteriaError is not null)
            {
                return criteriaError;
            }

            var result = await homeService.GetHomeAsync(
                criteria!,
                Request.ResolveContentLocale(),
                cancellationToken);
            return Ok(HomeContractMapper.ToHomeResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid home request.",
                exception.Message));
        }
    }

    [HttpGet("browse")]
    [ProducesResponseType(typeof(HomeBrowseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HomeBrowseResponse>> GetHomeBrowse(
        [FromQuery] string? type,
        [FromQuery] int? sectionSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = TryCreateCriteria(type, sectionSize, out var criteriaError);
            if (criteriaError is not null)
            {
                return criteriaError;
            }

            var result = await homeService.GetHomeBrowseAsync(
                criteria!,
                Request.ResolveContentLocale(),
                cancellationToken);
            return Ok(HomeContractMapper.ToHomeBrowseResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid home request.",
                exception.Message));
        }
    }

    [HttpGet("personalized")]
    [ProducesResponseType(typeof(HomePersonalizedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HomePersonalizedResponse>> GetHomePersonalized(
        [FromQuery] string? type,
        [FromQuery] int? sectionSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = TryCreateCriteria(type, sectionSize, out var criteriaError);
            if (criteriaError is not null)
            {
                return criteriaError;
            }

            var result = await homeService.GetHomePersonalizedAsync(
                criteria!,
                Request.ResolveContentLocale(),
                cancellationToken);
            return Ok(HomeContractMapper.ToHomePersonalizedResponse(result));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid home request.",
                exception.Message));
        }
    }

    private HomeCriteria? TryCreateCriteria(
        string? type,
        int? sectionSize,
        out ActionResult? errorResult)
    {
        errorResult = null;

        var typeValidation = HomeValidator.ValidateType(type);
        if (!typeValidation.IsValid)
        {
            errorResult = BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid home request.",
                typeValidation.ErrorMessage!));
            return null;
        }

        _ = HomeValidator.TryParseType(type, out var contentType);

        var homeOptions = options.Value;
        var effectiveSectionSize = sectionSize ?? homeOptions.DefaultSectionSize;
        var sectionSizeValidation = HomeValidator.ValidateSectionSize(
            effectiveSectionSize,
            1,
            homeOptions.MaximumSectionSize);

        if (!sectionSizeValidation.IsValid)
        {
            errorResult = BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid home request.",
                sectionSizeValidation.ErrorMessage!));
            return null;
        }

        return new HomeCriteria(contentType, effectiveSectionSize);
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
