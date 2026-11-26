using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Application.Services.StreamingLeaving;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Discovery;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/discovery/leaving-soon")]
public sealed class LeavingSoonController(IStreamingLeavingQueryService leavingQueryService) : ControllerBase
{
    private static readonly Dictionary<string, StreamingLeavingSort> Sorts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["leaving_asc"] = StreamingLeavingSort.LeavingAsc,
        ["rating_desc"] = StreamingLeavingSort.RatingDesc,
        ["rating_asc"] = StreamingLeavingSort.RatingAsc,
        ["release_desc"] = StreamingLeavingSort.ReleaseDesc,
        ["release_asc"] = StreamingLeavingSort.ReleaseAsc,
        ["title_asc"] = StreamingLeavingSort.TitleAsc,
        ["title_desc"] = StreamingLeavingSort.TitleDesc
    };

    [HttpGet]
    [ProducesResponseType(typeof(LeavingSoonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeavingSoonResponse>> Get(
        [FromQuery] string? watchRegion,
        [FromQuery] string? type,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string[]? genreId,
        [FromQuery] string? genreMatch,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] decimal? minRating,
        [FromQuery] decimal? maxRating,
        [FromQuery] int? minVoteCount,
        [FromQuery] int? minRuntimeMinutes,
        [FromQuery] int? maxRuntimeMinutes,
        [FromQuery] string? originalLanguage,
        [FromQuery] string? originCountry,
        [FromQuery] string[]? watchProviderId,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        // Region is required: a guessed default would spend that region's quota.
        var regionValidation = AdvancedDiscoverValidator.ValidateWatchRegion(watchRegion, required: true);
        if (!regionValidation.IsValid)
        {
            return Invalid(regionValidation.ErrorMessage!);
        }

        var region = WatchProviderRegionValidator.Normalize(watchRegion);
        if (!IsoCountryCodes.Contains(region))
        {
            return Invalid("Region must be a two-letter ISO country code.");
        }

        if (!AdvancedSearchValidator.TryParseType(type, out var contentType) ||
            contentType == SearchContentType.Person)
        {
            return Invalid("Type must be one of: movie, tv, all.");
        }

        var resolvedPage = page ?? SearchPaginationDefaults.DefaultPage;
        var resolvedPageSize = pageSize ?? SearchPaginationDefaults.DefaultPageSize;
        var paginationValidation = AdvancedSearchValidator.ValidatePagination(resolvedPage, resolvedPageSize);
        if (!paginationValidation.IsValid)
        {
            return Invalid(paginationValidation.ErrorMessage!);
        }

        var resolvedSort = StreamingLeavingSort.LeavingAsc;
        if (!string.IsNullOrWhiteSpace(sort) && !Sorts.TryGetValue(sort.Trim(), out resolvedSort))
        {
            return Invalid($"Sort must be one of: {string.Join(", ", Sorts.Keys)}.");
        }

        var criteria = new StreamingLeavingCriteria(
            region,
            contentType,
            resolvedPage,
            resolvedPageSize,
            AdvancedDiscoverValidator.ParseGenreIds(genreId),
            string.Equals(genreMatch, "all", StringComparison.OrdinalIgnoreCase),
            yearFrom,
            yearTo,
            minRating,
            maxRating,
            minVoteCount,
            minRuntimeMinutes,
            maxRuntimeMinutes,
            originalLanguage,
            originCountry,
            AdvancedDiscoverValidator.ParseWatchProviderIds(watchProviderId),
            resolvedSort);

        var result = await leavingQueryService.GetAsync(
            criteria,
            Request.ResolveContentLocale(),
            cancellationToken);

        return Ok(ToResponse(result));
    }

    private static LeavingSoonResponse ToResponse(StreamingLeavingResult result) =>
        new(
            result.Items.Select(entry => new LeavingSoonItemResponse(
                entry.Item.Id,
                entry.Item.Type,
                entry.Item.Title,
                entry.Item.OriginalTitle,
                entry.Item.Overview,
                entry.Item.PosterUrl,
                entry.Item.BackdropUrl,
                entry.Item.ReleaseDate,
                entry.Item.VoteAverage,
                entry.Item.VoteCount,
                entry.Item.Year,
                entry.Item.TmdbId,
                entry.Item.Genres ?? [],
                entry.LeavesAtUtc,
                entry.Platforms.Select(platform => new LeavingSoonPlatformResponse(
                    platform.ServiceId,
                    platform.Name,
                    platform.LeavesAtUtc,
                    platform.WatchLink)).ToList())).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.Page < result.TotalPages,
            result.Page > 1);

    private BadRequestObjectResult Invalid(string detail) =>
        BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid leaving soon request.",
            Detail = detail
        });
}
