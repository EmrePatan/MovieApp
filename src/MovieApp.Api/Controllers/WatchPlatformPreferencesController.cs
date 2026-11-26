using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Preferences;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

public sealed record UpdateWatchPlatformPreferencesRequest(IReadOnlyList<int>? ProviderIds);

[Authorize]
[ApiController]
[Route("api/watch-platforms")]
public sealed class WatchPlatformPreferencesController(IWatchPlatformPreferencesService preferences) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WatchPlatformPreferencesResult>> Get(CancellationToken cancellationToken)
    {
        try { return Ok(await preferences.GetAsync(cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Title = exception.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<WatchPlatformPreferencesResult>> Update(
        [FromBody] UpdateWatchPlatformPreferencesRequest request, CancellationToken cancellationToken)
    {
        if (request.ProviderIds is null)
        {
            return BadRequest(new ProblemDetails { Title = "providerIds is required." });
        }

        try { return Ok(await preferences.UpdateAsync(request.ProviderIds, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Title = exception.Message }); }
        catch (ValidationException exception) { return BadRequest(new ProblemDetails { Title = exception.Message }); }
    }
}
