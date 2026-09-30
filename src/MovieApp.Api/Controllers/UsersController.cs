using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.Errors;
using MovieApp.Api.Localization;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Contracts.Auth;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Contracts.Users;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public sealed class UsersController(
    IUserProfileService userProfileService,
    IUserCredentialMethodsService userCredentialMethodsService,
    IUserAvatarService userAvatarService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetCurrentProfile(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await userProfileService.GetCurrentProfileAsync(cancellationToken);
            return Ok(UserProfileContractMapper.ToUserProfileResponse(profile));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPost("me/avatar")]
    [EnableRateLimiting(AccountRateLimitPolicies.AvatarMutation)]
    [RequestSizeLimit(UserAvatarService.MaxUploadBytes)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UploadAvatar(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid avatar request.",
                "Avatar image file is required."));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var profile = await userAvatarService.UploadCustomAvatarAsync(
                stream,
                file.Length,
                cancellationToken);
            return Ok(UserProfileContractMapper.ToUserProfileResponse(profile));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid avatar request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpDelete("me/avatar")]
    [EnableRateLimiting(AccountRateLimitPolicies.AvatarMutation)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> RemoveAvatar(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await userAvatarService.RemoveCustomAvatarAsync(cancellationToken);
            return Ok(UserProfileContractMapper.ToUserProfileResponse(profile));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await userProfileService.UpdateDisplayNameAsync(request.DisplayName, cancellationToken);
            return Ok(UserProfileContractMapper.ToUserProfileResponse(profile));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid profile request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPut("me/email")]
    [EnableRateLimiting(AccountRateLimitPolicies.ChangeEmail)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> ChangeEmail(
        [FromBody] ChangeEmailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var proof = new AccountReauthenticationProof(
                request.CurrentPassword,
                request.ReauthProvider,
                request.ReauthIdentityToken);
            var result = await userCredentialMethodsService.RequestEmailChangeAsync(
                new RequestEmailChangeCommand(
                    request.Email,
                    proof,
                    Request.ResolveContentLocale()),
                cancellationToken);

            return Ok(new MessageResponse(result.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(ApiProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Invalid email change request.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (ConflictException exception)
        {
            return Conflict(ApiProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Email change conflict.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(ApiProblemDetailsHelper.Create(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (NotFoundException exception)
        {
            return NotFound(ApiProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPost("me/email/pending/resend")]
    [EnableRateLimiting(AccountRateLimitPolicies.ChangeEmail)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> ResendPendingEmailChange(CancellationToken cancellationToken)
    {
        try
        {
            var result = await userCredentialMethodsService.ResendPendingEmailChangeAsync(
                Request.ResolveContentLocale(),
                cancellationToken);

            return Ok(new MessageResponse(result.Message));
        }
        catch (ValidationException exception)
        {
            return BadRequest(ApiProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Invalid pending email change request.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(ApiProblemDetailsHelper.Create(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (NotFoundException exception)
        {
            return NotFound(ApiProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPost("me/linked-providers")]
    [EnableRateLimiting(AccountRateLimitPolicies.ChangePassword)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> LinkExternalLogin(
        [FromBody] LinkExternalLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var proof = new AccountReauthenticationProof(
                request.CurrentPassword,
                request.ReauthProvider,
                request.ReauthIdentityToken);
            var profile = await userCredentialMethodsService.LinkExternalLoginAsync(
                new LinkExternalLoginCommand(
                    proof,
                    request.TargetProvider,
                    request.TargetIdentityToken),
                cancellationToken);

            return Ok(UserProfileContractMapper.ToUserProfileResponse(profile));
        }
        catch (ValidationException exception)
        {
            return BadRequest(ApiProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Invalid link request.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (ConflictException exception)
        {
            return Conflict(ApiProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Link conflict.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(ApiProblemDetailsHelper.Create(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (NotFoundException exception)
        {
            return NotFound(ApiProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpDelete("me/linked-providers/{provider}")]
    [EnableRateLimiting(AccountRateLimitPolicies.ChangePassword)]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> UnlinkExternalLogin(
        string provider,
        [FromBody] UnlinkExternalLoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var proof = new AccountReauthenticationProof(
                request.CurrentPassword,
                request.ReauthProvider,
                request.ReauthIdentityToken);
            var profile = await userCredentialMethodsService.UnlinkExternalLoginAsync(
                new UnlinkExternalLoginCommand(provider, proof),
                cancellationToken);

            return Ok(UserProfileContractMapper.ToUserProfileResponse(profile));
        }
        catch (ValidationException exception)
        {
            return BadRequest(ApiProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Invalid unlink request.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (ConflictException exception)
        {
            return Conflict(ApiProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Unlink conflict.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(ApiProblemDetailsHelper.Create(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (NotFoundException exception)
        {
            return NotFound(ApiProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPost("me/password")]
    [EnableRateLimiting(AccountRateLimitPolicies.ChangePassword)]
    [ProducesResponseType(typeof(UserProfileAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileAuthResponse>> CreatePassword(
        [FromBody] CreatePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await userCredentialMethodsService.CreatePasswordAsync(
                new CreatePasswordCommand(
                    request.NewPassword,
                    request.Provider,
                    request.IdentityToken),
                cancellationToken);

            return Ok(UserProfileContractMapper.ToUserProfileAuthResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(ApiProblemDetailsHelper.Create(
                StatusCodes.Status400BadRequest,
                "Invalid create password request.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (ConflictException exception)
        {
            return Conflict(ApiProblemDetailsHelper.Create(
                StatusCodes.Status409Conflict,
                "Create password conflict.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(ApiProblemDetailsHelper.Create(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message,
                exception.ErrorCode));
        }
        catch (NotFoundException exception)
        {
            return NotFound(ApiProblemDetailsHelper.Create(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpPut("me/password")]
    [EnableRateLimiting(AccountRateLimitPolicies.ChangePassword)]
    [ProducesResponseType(typeof(UserProfileAuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<UserProfileAuthResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await userProfileService.ChangePasswordAsync(
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken);

            return Ok(UserProfileContractMapper.ToUserProfileAuthResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid password change request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found.",
                exception.Message));
        }
    }

    [HttpGet("me/statistics")]
    [ProducesResponseType(typeof(UserStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserStatisticsResponse>> GetStatistics(
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await userProfileService.GetStatisticsAsync(timeZone, cancellationToken);
            return Ok(UserProfileContractMapper.ToUserStatisticsResponse(
                statistics,
                Request.ResolveContentLocale()));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpDelete("me")]
    [EnableRateLimiting(AccountRateLimitPolicies.DeleteAccount)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await userProfileService.DeleteAccountAsync(
                new DeleteAccountCommand(
                    request.CurrentPassword,
                    request.Provider,
                    request.IdentityToken),
                cancellationToken);
            return NoContent();
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid account deletion request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found.",
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
