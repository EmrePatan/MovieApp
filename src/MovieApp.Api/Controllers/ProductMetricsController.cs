using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.ProductMetrics;
using MovieApp.Contracts.ProductMetrics;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/product-metrics")]
public sealed class ProductMetricsController(IIncrementProductMetricService incrementProductMetricService)
    : ControllerBase
{
    [HttpPost("increment")]
    [EnableRateLimiting(ProductMetricsRateLimitPolicies.Increment)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Increment(
        [FromBody] IncrementProductMetricRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await incrementProductMetricService.IncrementAsync(request.MetricName, cancellationToken);
            return NoContent();
        }
        catch (ValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid product metric request.",
                Detail = exception.Message,
            });
        }
    }
}
