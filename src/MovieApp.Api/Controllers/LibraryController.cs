using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Library;

namespace MovieApp.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/library")]
public sealed class LibraryController(
    ILibraryService libraryService,
    ILibraryActionStatusService libraryActionStatusService) : ControllerBase
{
    [HttpGet("actions")]
    [ProducesResponseType(typeof(LibraryActionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LibraryActionStatusResponse>> GetActionStatus(
        [FromQuery] string? mediaType,
        [FromQuery] Guid contentId,
        [FromQuery] Guid? episodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await libraryActionStatusService.GetAsync(
                mediaType,
                contentId,
                episodeId,
                cancellationToken);

            return Ok(new LibraryActionStatusResponse(
                result.MediaType,
                result.ContentId,
                result.IsFavorited,
                result.IsInWatchlist,
                result.WatchlistIds,
                result.IsFollowing,
                result.NotifyNewSeasons,
                result.NotifyNewEpisodes,
                result.BaselineEstablished,
                result.IsWatched,
                result.WatchedAt));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid library action request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
                exception.Message));
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(LibraryListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LibraryListResponse>> GetLibrary(
        [FromQuery] string? category,
        [FromQuery] string? mediaType,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        try
        {
            var categoryValidation = LibraryValidator.ValidateCategory(category);
            if (!categoryValidation.IsValid)
            {
                throw new ValidationException(categoryValidation.ErrorMessage!);
            }

            var mediaTypeValidation = LibraryValidator.ValidateMediaType(mediaType);
            if (!mediaTypeValidation.IsValid)
            {
                throw new ValidationException(mediaTypeValidation.ErrorMessage!);
            }

            _ = LibraryValidator.TryParseCategory(category, out var libraryCategory);
            _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);

            var criteria = new LibraryCriteria(
                libraryCategory,
                contentType,
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? LibraryValidator.DefaultPageSize,
                string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim());

            var result = await libraryService.GetLibraryAsync(criteria, cancellationToken);

            return Ok(LibraryContractMapper.ToLibraryListResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid library request.",
                exception.Message));
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(CreateProblemDetails(
                StatusCodes.Status401Unauthorized,
                "Authentication required.",
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
