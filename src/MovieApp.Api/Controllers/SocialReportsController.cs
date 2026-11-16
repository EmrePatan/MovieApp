using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social/reports")]
public sealed class SocialReportsController(ISocialReportService reports) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SocialReportResult>> Create([FromBody] CreateSocialReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reports.CreateAsync(request.TargetType, request.TargetId, request.Reason,
                request.Details, cancellationToken);
            return CreatedAtAction(nameof(Create), new { id = result.Id }, result);
        }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException exception) { return NotFound(new ProblemDetails { Detail = exception.Message }); }
        catch (ConflictException exception) { return Conflict(new ProblemDetails { Detail = exception.Message }); }
        catch (ValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }
}

public sealed record CreateSocialReportRequest(string TargetType, Guid TargetId, string Reason, string? Details);