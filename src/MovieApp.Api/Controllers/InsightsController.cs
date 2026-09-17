using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Insights;
using MovieApp.Contracts.Insights;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/insights")]
public sealed class InsightsController(
    IInsightsSummaryService insightsSummaryService,
    IInsightsAnalyticsService insightsAnalyticsService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InsightsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InsightsSummaryResponse>> GetSummary(
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await insightsSummaryService.GetSummaryAsync(timeZone, cancellationToken);
            return Ok(InsightsContractMapper.ToInsightsSummaryResponse(summary));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(InsightsAnalyticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InsightsAnalyticsResponse>> GetAnalytics(
        [FromQuery] string timeZone,
        CancellationToken cancellationToken)
    {
        try
        {
            var analytics = await insightsAnalyticsService.GetAnalyticsAsync(timeZone, cancellationToken);
            return Ok(InsightsContractMapper.ToInsightsAnalyticsResponse(analytics));
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
                "Invalid insights analytics request.",
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
