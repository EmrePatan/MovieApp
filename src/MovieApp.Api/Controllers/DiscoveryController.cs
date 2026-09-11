using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Search;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/discovery")]
public sealed class DiscoveryController(IDiscoveryService discoveryService) : ControllerBase
{
    [HttpGet("popular")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> GetPopular(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildDiscoveryCriteria(page, pageSize, type);
            var result = await discoveryService.GetPopularAsync(criteria, cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid discovery request.",
                exception.Message));
        }
    }

    [HttpGet("trending")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> GetTrending(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildDiscoveryCriteria(page, pageSize, type);
            var result = await discoveryService.GetTrendingAsync(criteria, cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid discovery request.",
                exception.Message));
        }
    }

    private static DiscoveryCriteria BuildDiscoveryCriteria(int? page, int? pageSize, string? type)
    {
        var typeValidation = AdvancedSearchValidator.ValidateType(type);
        if (!typeValidation.IsValid)
        {
            throw new ValidationException(typeValidation.ErrorMessage!);
        }

        _ = AdvancedSearchValidator.TryParseType(type, out var contentType);

        return new DiscoveryCriteria(
            contentType,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
