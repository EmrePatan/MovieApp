using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Contracts.AiRecommendations;

namespace MovieApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai/recommendations")]
public sealed class AiRecommendationsController(
    IAiMovieRecommendationService recommendationService,
    ICurrentUser currentUser,
    IOptions<AiRecommendationOptions> options) : ControllerBase
{
    [HttpGet("quota")]
    [ProducesResponseType(typeof(AiRecommendationQuotaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AiRecommendationQuotaResponse>> GetQuota(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var remaining = await recommendationService.GetRemainingQuotaAsync(
            currentUser.UserId.Value,
            cancellationToken);

        return Ok(new AiRecommendationQuotaResponse(remaining, options.Value.UserDailyMessageLimit));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AiRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiRecommendationResponse>> GetRecommendations(
        [FromBody] AiRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, validationError));
        }

        try
        {
            var result = await recommendationService.GetRecommendationsAsync(
                currentUser.UserId.Value,
                request.Message.Trim(),
                request.SessionId,
                Request.ResolveContentLocale(),
                cancellationToken);

            var response = AiRecommendationContractMapper.ToResponse(result);
            if (result.ReturnedCount == 0)
            {
                return UnprocessableEntity(response);
            }

            return Ok(response);
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, exception.Message));
        }
        catch (AiRecommendationEntitlementException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(StatusCodes.Status403Forbidden, exception.Message));
        }
        catch (AiRecommendationQuotaExceededException exception)
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                CreateProblemDetails(StatusCodes.Status429TooManyRequests, exception.Message));
        }
        catch (AiRecommendationNoValidResultsException exception)
        {
            return UnprocessableEntity(CreateProblemDetails(StatusCodes.Status422UnprocessableEntity, exception.Message));
        }
        catch (AiRecommendationProviderUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(StatusCodes.Status503ServiceUnavailable, exception.Message));
        }
        catch (AiRecommendationInfrastructureUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(StatusCodes.Status503ServiceUnavailable, exception.Message));
        }
    }

    private static string? ValidateRequest(AiRecommendationRequest request)
    {
        if (request.Message is null)
        {
            return "Message is required.";
        }

        var message = request.Message.Trim();
        if (message.Length < 3)
        {
            return "Message must be at least 3 characters.";
        }

        if (message.Length > 500)
        {
            return "Message must be at most 500 characters.";
        }

        return null;
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string detail) =>
        new()
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status400BadRequest => "Invalid request.",
                StatusCodes.Status403Forbidden => "Premium required.",
                StatusCodes.Status422UnprocessableEntity => "No valid recommendations.",
                StatusCodes.Status429TooManyRequests => AiRecommendationQuotaMessages.DailyLimitTitle,
                StatusCodes.Status503ServiceUnavailable => "Service unavailable.",
                _ => "Request failed."
            },
            Detail = detail
        };
}
