using Microsoft.AspNetCore.Mvc;
using MovieApp.Contracts.Health;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Get()
    {
        var response = new HealthCheckResponse(
            Status: "Healthy",
            Timestamp: DateTimeOffset.UtcNow,
            Environment: environment.EnvironmentName);

        return Ok(response);
    }
}
