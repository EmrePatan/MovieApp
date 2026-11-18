using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/social/privacy")]
public sealed class SocialPrivacyController(ISocialPrivacyService privacyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SocialPrivacyResult>> Get(CancellationToken cancellationToken)
    {
        try { return Ok(await privacyService.GetAsync(cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPut]
    public async Task<ActionResult<SocialPrivacyResult>> Update(
        [FromBody] SocialPrivacyResult request, CancellationToken cancellationToken)
    {
        try { return Ok(await privacyService.UpdateAsync(request, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
    }
}
