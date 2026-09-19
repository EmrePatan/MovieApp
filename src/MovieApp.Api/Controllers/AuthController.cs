using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Api.RateLimiting;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;
using MovieApp.Contracts.Auth;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IRegisterUserService registerUserService,
    ILoginUserService loginUserService,
    ISocialAuthService socialAuthService,
    IGetCurrentUserService getCurrentUserService,
    IForgotPasswordService forgotPasswordService,
    IResetPasswordService resetPasswordService,
    IVerifyEmailService verifyEmailService,
    IResendVerificationService resendVerificationService) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimitPolicies.Register)]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await registerUserService.RegisterAsync(
                AuthContractMapper.ToRegisterUserRequest(request, Request.ResolveContentLocale()),
                cancellationToken);

            return Created(string.Empty, AuthContractMapper.ToRegisterResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid registration request.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Registration conflict.",
                exception.Message));
        }
    }

    [HttpPost("social")]
    [EnableRateLimiting(AuthRateLimitPolicies.Social)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Social(
        [FromBody] SocialAuthRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await socialAuthService.AuthenticateAsync(
                AuthContractMapper.ToSocialAuthRequest(request),
                cancellationToken);

            return Ok(AuthContractMapper.ToAuthResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid social authentication request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                exception.Message));
        }
        catch (ConflictException exception)
        {
            return Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Social authentication conflict.",
                exception.Message));
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimitPolicies.Login)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await loginUserService.LoginAsync(
                AuthContractMapper.ToLoginUserRequest(request),
                cancellationToken);

            return Ok(AuthContractMapper.ToAuthResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid login request.",
                exception.Message));
        }
        catch (EmailNotVerifiedException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                exception.Message,
                EmailNotVerifiedException.ErrorCode));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                exception.Message));
        }
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting(AuthRateLimitPolicies.VerifyEmail)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await verifyEmailService.VerifyEmailAsync(
                AuthContractMapper.ToVerifyEmailRequest(request),
                cancellationToken);

            return Ok(AuthContractMapper.ToAuthResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid verification request.",
                exception.Message));
        }
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting(AuthRateLimitPolicies.ResendVerification)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await resendVerificationService.ResendVerificationAsync(
                AuthContractMapper.ToResendVerificationRequest(request, Request.ResolveContentLocale()),
                cancellationToken);

            return Ok(AuthContractMapper.ToMessageResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid resend verification request.",
                exception.Message));
        }
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.ForgotPassword)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await forgotPasswordService.ForgotPasswordAsync(
                AuthContractMapper.ToForgotPasswordRequest(request, Request.ResolveContentLocale()),
                cancellationToken);

            return Ok(AuthContractMapper.ToMessageResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid forgot password request.",
                exception.Message));
        }
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(AuthRateLimitPolicies.ResetPassword)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MessageResponse>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await resetPasswordService.ResetPasswordAsync(
                AuthContractMapper.ToResetPasswordRequest(request),
                cancellationToken);

            return Ok(AuthContractMapper.ToMessageResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid reset password request.",
                exception.Message));
        }
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        try
        {
            var user = await getCurrentUserService.GetCurrentUserAsync(cancellationToken);
            return Ok(AuthContractMapper.ToCurrentUserResponse(user));
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

    private static ProblemDetails CreateProblemDetails(
        int statusCode,
        string title,
        string detail,
        string? code = null)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        if (!string.IsNullOrWhiteSpace(code))
        {
            problemDetails.Extensions["code"] = code;
        }

        return problemDetails;
    }
}
