using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Services.CatalogFollows;
using MovieApp.Contracts.CatalogFollows;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/follows/catalog")]
public sealed class CatalogFollowController(IGetCatalogFollowsService getCatalogFollowsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CatalogFollowsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CatalogFollowsResponse>> GetFollowedCatalog(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await getCatalogFollowsService.GetAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(CatalogFollowContractMapper.ToFollowsResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid pagination request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
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

[ApiController]
[Route("api/catalog/upcoming")]
public sealed class CatalogUpcomingController(IGetCatalogUpcomingService getCatalogUpcomingService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CatalogUpcomingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CatalogUpcomingResponse>> GetUpcomingCatalog(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        if (!TryParseScope(scope, out var parsedScope))
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid scope request.",
                "Scope must be 'catalog' or 'followed'."));
        }

        try
        {
            var result = await getCatalogUpcomingService.GetAsync(
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                parsedScope,
                cancellationToken);

            return Ok(CatalogFollowContractMapper.ToUpcomingResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid pagination request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    private static bool TryParseScope(string? scope, out CatalogUpcomingScope parsedScope)
    {
        if (string.IsNullOrWhiteSpace(scope) ||
            string.Equals(scope, "catalog", StringComparison.OrdinalIgnoreCase))
        {
            parsedScope = CatalogUpcomingScope.Catalog;
            return true;
        }

        if (string.Equals(scope, "followed", StringComparison.OrdinalIgnoreCase))
        {
            parsedScope = CatalogUpcomingScope.Followed;
            return true;
        }

        parsedScope = CatalogUpcomingScope.Catalog;
        return false;
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
