using Microsoft.AspNetCore.Mvc;
using MovieApp.Contracts.Health;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IWebHostEnvironment environment) : ControllerBase
{
    // Liveness only. Does not expose secrets. Readiness (/health/ready) may reveal dependency
    // availability to load balancers; restrict at the edge if detailed probe data is undesirable.
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Get() => Ok(CreateHealthyResponse());

    [HttpGet("live")]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> GetLive() => Ok(CreateHealthyResponse());

    private HealthCheckResponse CreateHealthyResponse() =>
        new(
            Status: "Healthy",
            Timestamp: DateTimeOffset.UtcNow,
            Environment: environment.EnvironmentName);
}
