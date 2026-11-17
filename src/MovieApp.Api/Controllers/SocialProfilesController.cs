using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;\nusing MovieApp.Api.Localization;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Contracts.Users;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/social/profiles")]
public sealed class SocialProfilesController(ISocialProfileService socialProfileService, ISocialFeedService socialFeedService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<SocialUserSummaryResult>>> Search(
        [FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await socialProfileService.SearchUsersAsync(q ?? string.Empty, page, pageSize, cancellationToken));

    [AllowAnonymous]
    [HttpGet("{userId:guid}/followers")]
    public async Task<ActionResult<IReadOnlyList<SocialUserSummaryResult>>> Followers(
        Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await socialProfileService.GetFollowersAsync(userId, page, pageSize, cancellationToken)); }
        catch (NotFoundException) { return NotFound(new ProblemDetails { Title = "User not found." }); }
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/following")]
    public async Task<ActionResult<IReadOnlyList<SocialUserSummaryResult>>> Following(
        Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await socialProfileService.GetFollowingAsync(userId, page, pageSize, cancellationToken)); }
        catch (NotFoundException) { return NotFound(new ProblemDetails { Title = "User not found." }); }
    }

    [Authorize]
    [HttpGet("{userId:guid}/activities")]
    public async Task<ActionResult<SocialFeedPageResult>> Activities(
        Guid userId, [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await socialFeedService.GetUserActivitiesAsync(userId, cursor, pageSize, cancellationToken)); }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException) { return NotFound(new ProblemDetails { Title = "User not found." }); }
        catch (ValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(PublicSocialProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicSocialProfileResponse>> GetProfile(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await socialProfileService.GetProfileAsync(userId, cancellationToken);
            return Ok(new PublicSocialProfileResponse(profile.Id, profile.UserName, profile.DisplayName,
                profile.CreatedAt, profile.AvatarUrl, profile.IsPrivate, profile.FollowStatus,
                profile.FollowersCount, profile.FollowingCount));
        }
        catch (NotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "User not found." });
        }
    }
}
