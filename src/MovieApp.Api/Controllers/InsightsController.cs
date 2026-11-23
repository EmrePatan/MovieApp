using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Insights;
using MovieApp.Contracts.Insights;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/insights")]
public sealed class InsightsController(
    IInsightsV3Service insightsV3Service,
    ICurrentUser currentUser,
    ISocialProfileService socialProfileService) : ControllerBase
{
    [HttpGet("v3")]
    [ProducesResponseType(typeof(InsightsV3Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InsightsV3Response>> GetV3(
        [FromQuery] string timeZone,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        try
        {
            var insights = await insightsV3Service.GetInsightsV3Async(timeZone, year, cancellationToken);
            return Ok(InsightsContractMapper.ToInsightsV3Response(
                insights,
                Request.ResolveContentLocale()));
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
                "Invalid insights request.",
                exception.Message));
        }
    }

    [HttpGet("compare/{userId:guid}")]
    [ProducesResponseType(typeof(InsightsCompareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InsightsCompareResponse>> Compare(
        Guid userId,
        [FromQuery] string timeZone,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!currentUser.IsAuthenticated || currentUser.UserId is not { } myId)
            {
                return Unauthorized(CreateProblemDetails(
                    StatusCodes.Status401Unauthorized,
                    "Authentication required.",
                    "Sign in to compare insights."));
            }

            if (myId == userId)
            {
                return BadRequest(CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "Invalid insights request.",
                    "Cannot compare a profile with itself."));
            }

            // Same visibility rule as that profile's reviews: private accounts and hidden
            // activity stay hidden from non-followers.
            if (!await socialProfileService.CanViewReviewsAsync(userId, cancellationToken))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblemDetails(
                        StatusCodes.Status403Forbidden,
                        "Profile is not visible.",
                        "This profile's activity is not visible to you."));
            }

            var locale = Request.ResolveContentLocale();
            var mine = InsightsContractMapper.ToInsightsV3Response(
                await insightsV3Service.GetInsightsV3ForUserAsync(myId, timeZone, null, cancellationToken),
                locale);
            var theirs = InsightsContractMapper.ToInsightsV3Response(
                await insightsV3Service.GetInsightsV3ForUserAsync(userId, timeZone, null, cancellationToken),
                locale);
            return Ok(InsightsCompareMapper.Build(mine, theirs));
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
                "Invalid insights request.",
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
