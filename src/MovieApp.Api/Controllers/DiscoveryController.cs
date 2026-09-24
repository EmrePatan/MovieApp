using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Discovery;
using MovieApp.Contracts.Search;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/discovery")]
public sealed class DiscoveryController(
    IDiscoveryService discoveryService,
    IDiscoverBrowseService discoverBrowseService,
    IAdvancedDiscoverService advancedDiscoverService,
    IDiscoveryWatchProvidersService discoveryWatchProvidersService,
    INowInTheatersService nowInTheatersService,
    IOnTvThisWeekService onTvThisWeekService,
    IWorldCinemaService worldCinemaService,
    IExplorePreviewService explorePreviewService,
    IPickSomethingService pickSomethingService) : ControllerBase
{
    [HttpGet("pick-something")]
    [ProducesResponseType(typeof(PickSomethingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PickSomethingResponse>> GetPickSomething(
        [FromQuery] string? mediaType,
        [FromQuery] string[]? excludeIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var mediaTypeValidation = PickSomethingValidator.ValidateMediaType(mediaType);
            if (!mediaTypeValidation.IsValid)
            {
                throw new ValidationException(mediaTypeValidation.ErrorMessage!);
            }

            _ = RecommendationValidator.TryParseType(mediaType, out var contentType);

            var criteria = new PickSomethingCriteria(
                contentType,
                PickSomethingValidator.ParseSessionExcludedIds(excludeIds));

            var item = await pickSomethingService.PickAsync(
                criteria,
                Request.ResolveContentLocale(),
                cancellationToken);

            return Ok(new PickSomethingResponse(
                item is null ? null : RecommendationContractMapper.ToRecommendationItemResponse(item)));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid pick something request.",
                exception.Message));
        }
    }

    [HttpGet("explore-preview")]
    [ProducesResponseType(typeof(ExplorePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExplorePreviewResponse>> GetExplorePreview(
        [FromQuery] int? sectionSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = new ExplorePreviewCriteria(
                sectionSize ?? SearchPaginationDefaults.DefaultPageSize);
            var result = await explorePreviewService.GetPreviewAsync(
                criteria,
                Request.ResolveContentLocale(),
                cancellationToken);

            return Ok(new ExplorePreviewResponse(
                SearchContractMapper.ToSearchResponse(result.Trending),
                SearchContractMapper.ToSearchResponse(result.TopRated),
                SearchContractMapper.ToSearchResponse(result.NewReleases)));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid explore preview request.",
                exception.Message));
        }
    }

    [HttpGet("popular")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> GetPopular(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildDiscoveryCriteria(page, pageSize, type);
            var result = await discoveryService.GetPopularAsync(criteria, Request.ResolveContentLocale(), cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid discovery request.",
                exception.Message));
        }
    }

    [HttpGet("trending")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponse>> GetTrending(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildDiscoveryCriteria(page, pageSize, type);
            var result = await discoveryService.GetTrendingAsync(criteria, Request.ResolveContentLocale(), cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid discovery request.",
                exception.Message));
        }
    }

    [HttpGet("watch-providers")]
    [ProducesResponseType(typeof(DiscoveryWatchProvidersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DiscoveryWatchProvidersResponse>> GetWatchProviders(
        [FromQuery(Name = "mediaType")] string mediaType,
        [FromQuery] string watchRegion,
        CancellationToken cancellationToken)
    {
        try
        {
            var mediaTypeValidation = AdvancedDiscoverValidator.ValidateMediaType(mediaType);
            if (!mediaTypeValidation.IsValid)
            {
                throw new ValidationException(mediaTypeValidation.ErrorMessage!);
            }

            _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);

            var providers = await discoveryWatchProvidersService.GetWatchProvidersAsync(
                contentType,
                watchRegion,
                cancellationToken);

            var normalizedRegion = WatchProviderRegionValidator.Normalize(watchRegion);
            return Ok(DiscoveryContractMapper.ToWatchProvidersResponse(
                normalizedRegion,
                contentType.ToString().ToLowerInvariant(),
                providers));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid watch provider request.",
                exception.Message));
        }
        catch (SearchProviderUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "Watch providers are temporarily unavailable.",
                    "Streaming provider data could not be loaded right now. Please try again."));
        }
    }

    [HttpGet("now-in-theaters")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SearchResponse>> GetNowInTheaters(
        [FromQuery] string? releaseRegion,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildNowInTheatersCriteria(releaseRegion, page, pageSize);
            var result = await nowInTheatersService.GetNowInTheatersAsync(
                criteria,
                Request.ResolveContentLocale(),
                cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid now in theaters request.",
                exception.Message));
        }
        catch (SearchProviderUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "Now in theaters is temporarily unavailable.",
                    "Theatrical listings could not be loaded right now. Please try again."));
        }
    }

    [HttpGet("world-cinema")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SearchResponse>> GetWorldCinema(
        [FromQuery(Name = "mediaType")] string? mediaType,
        [FromQuery(Name = "originCountry")] string? originCountry,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildWorldCinemaCriteria(mediaType, originCountry, sort, page, pageSize);
            var result = await worldCinemaService.GetWorldCinemaAsync(
                criteria,
                Request.ResolveContentLocale(),
                cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid world cinema request.",
                exception.Message));
        }
        catch (SearchProviderUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "World cinema is temporarily unavailable.",
                    "World cinema listings could not be loaded right now. Please try again."));
        }
    }

    [HttpGet("on-tv-this-week")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SearchResponse>> GetOnTvThisWeek(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildOnTvThisWeekCriteria(page, pageSize);
            var result = await onTvThisWeekService.GetOnTvThisWeekAsync(
                criteria,
                Request.ResolveContentLocale(),
                cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid on TV this week request.",
                exception.Message));
        }
        catch (SearchProviderUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "On TV this week is temporarily unavailable.",
                    "TV airing listings could not be loaded right now. Please try again."));
        }
    }

    [HttpGet("advanced")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SearchResponse>> AdvancedDiscover(
        [FromQuery(Name = "mediaType")] string mediaType,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string[]? genreId,
        [FromQuery] string? genreMatch,
        [FromQuery] int? year,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        [FromQuery] decimal? minRating,
        [FromQuery] decimal? maxRating,
        [FromQuery] int? minVoteCount,
        [FromQuery] int? minRuntimeMinutes,
        [FromQuery] int? maxRuntimeMinutes,
        [FromQuery] string? originalLanguage,
        [FromQuery(Name = "originCountry")] string? originCountry,
        [FromQuery] string? certification,
        [FromQuery] string? certificationCountry,
        [FromQuery] string[]? releaseType,
        [FromQuery] string? watchRegion,
        [FromQuery] string[]? watchProviderId,
        [FromQuery] string[]? watchMonetizationType,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildAdvancedDiscoverCriteria(
                mediaType,
                page,
                pageSize,
                genreId,
                genreMatch,
                year,
                yearFrom,
                yearTo,
                minRating,
                maxRating,
                minVoteCount,
                minRuntimeMinutes,
                maxRuntimeMinutes,
                originalLanguage,
                originCountry,
                certification,
                certificationCountry,
                releaseType,
                watchRegion,
                watchProviderId,
                watchMonetizationType,
                sort);

            var result = await advancedDiscoverService.DiscoverAsync(criteria, Request.ResolveContentLocale(), cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid advanced discover request.",
                exception.Message));
        }
    }

    [HttpGet("browse")]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SearchResponse>> Browse(
        [FromQuery] string mode,
        [FromQuery] string? type,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string[]? genreId,
        [FromQuery] int? year,
        [FromQuery] decimal? minRating,
        [FromQuery] string? language,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        try
        {
            var criteria = BuildBrowseCriteria(
                mode,
                type,
                page,
                pageSize,
                genreId,
                year,
                minRating,
                language,
                sort);

            var result = await discoverBrowseService.BrowseAsync(criteria, Request.ResolveContentLocale(), cancellationToken);
            return Ok(SearchContractMapper.ToSearchResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid discovery browse request.",
                exception.Message));
        }
    }

    private static WorldCinemaCriteria BuildWorldCinemaCriteria(
        string? mediaType,
        string? originCountry,
        string? sort,
        int? page,
        int? pageSize)
    {
        var mediaTypeValidation = WorldCinemaValidator.ValidateMediaTypeValue(mediaType ?? "movie");
        if (!mediaTypeValidation.IsValid)
        {
            throw new ValidationException(mediaTypeValidation.ErrorMessage!);
        }

        var originCountryValidation = WorldCinemaValidator.ValidateRequiredOriginCountry(originCountry);
        if (!originCountryValidation.IsValid)
        {
            throw new ValidationException(originCountryValidation.ErrorMessage!);
        }

        var sortValidation = WorldCinemaValidator.ValidateSortValue(sort);
        if (!sortValidation.IsValid)
        {
            throw new ValidationException(sortValidation.ErrorMessage!);
        }

        _ = AdvancedSearchValidator.TryParseType(mediaType ?? "movie", out var contentType);
        _ = AdvancedDiscoverValidator.TryParseSort(sort, out var discoverSort);

        var criteria = new WorldCinemaCriteria(
            contentType,
            originCountry!.Trim(),
            discoverSort,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);

        var validation = WorldCinemaValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        return criteria;
    }

    private static OnTvThisWeekCriteria BuildOnTvThisWeekCriteria(int? page, int? pageSize)
    {
        var criteria = new OnTvThisWeekCriteria(
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);

        var validation = OnTvThisWeekValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        return criteria;
    }

    private static NowInTheatersCriteria BuildNowInTheatersCriteria(
        string? releaseRegion,
        int? page,
        int? pageSize)
    {
        var normalizedRegion = string.IsNullOrWhiteSpace(releaseRegion)
            ? WatchProviderRegionValidator.DefaultRegion
            : WatchProviderRegionValidator.Normalize(releaseRegion);

        var criteria = new NowInTheatersCriteria(
            normalizedRegion,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);

        var validation = NowInTheatersValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        return criteria;
    }

    private static AdvancedDiscoverCriteria BuildAdvancedDiscoverCriteria(
        string mediaType,
        int? page,
        int? pageSize,
        string[]? genreId,
        string? genreMatch,
        int? year,
        int? yearFrom,
        int? yearTo,
        decimal? minRating,
        decimal? maxRating,
        int? minVoteCount,
        int? minRuntimeMinutes,
        int? maxRuntimeMinutes,
        string? originalLanguage,
        string? originCountry,
        string? certification,
        string? certificationCountry,
        string[]? releaseType,
        string? watchRegion,
        string[]? watchProviderId,
        string[]? watchMonetizationType,
        string? sort)
    {
        var mediaTypeValidation = AdvancedDiscoverValidator.ValidateMediaType(mediaType);
        if (!mediaTypeValidation.IsValid)
        {
            throw new ValidationException(mediaTypeValidation.ErrorMessage!);
        }

        var sortValidation = AdvancedDiscoverValidator.ValidateSort(sort);
        if (!sortValidation.IsValid)
        {
            throw new ValidationException(sortValidation.ErrorMessage!);
        }

        var monetizationValidation =
            AdvancedDiscoverValidator.ValidateWatchMonetizationTypeValues(watchMonetizationType);
        if (!monetizationValidation.IsValid)
        {
            throw new ValidationException(monetizationValidation.ErrorMessage!);
        }

        var genreMatchValidation = AdvancedDiscoverValidator.ValidateGenreMatchValue(genreMatch);
        if (!genreMatchValidation.IsValid)
        {
            throw new ValidationException(genreMatchValidation.ErrorMessage!);
        }

        var releaseTypeValidation = AdvancedDiscoverValidator.ValidateReleaseTypeValues(releaseType);
        if (!releaseTypeValidation.IsValid)
        {
            throw new ValidationException(releaseTypeValidation.ErrorMessage!);
        }

        _ = AdvancedSearchValidator.TryParseType(mediaType, out var contentType);
        _ = AdvancedDiscoverValidator.TryParseSort(sort, out var discoverSort);
        _ = AdvancedDiscoverValidator.TryParseGenreMatch(genreMatch, out var parsedGenreMatch);

        var criteria = new AdvancedDiscoverCriteria(
            contentType,
            AdvancedDiscoverValidator.ParseGenreIds(genreId),
            parsedGenreMatch,
            year,
            yearFrom,
            yearTo,
            minRating,
            maxRating,
            minVoteCount,
            minRuntimeMinutes,
            maxRuntimeMinutes,
            originalLanguage,
            originCountry,
            string.IsNullOrWhiteSpace(certification) ? null : certification.Trim(),
            string.IsNullOrWhiteSpace(certificationCountry) ? null : certificationCountry.Trim(),
            AdvancedDiscoverValidator.ParseReleaseTypes(releaseType),
            string.IsNullOrWhiteSpace(watchRegion)
                ? null
                : WatchProviderRegionValidator.Normalize(watchRegion),
            AdvancedDiscoverValidator.ParseWatchProviderIds(watchProviderId),
            AdvancedDiscoverValidator.ParseWatchMonetizationTypes(watchMonetizationType),
            discoverSort,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);

        var validation = AdvancedDiscoverValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        return criteria;
    }

    private static DiscoveryCriteria BuildDiscoveryCriteria(int? page, int? pageSize, string? type)
    {
        var typeValidation = AdvancedSearchValidator.ValidateType(type);
        if (!typeValidation.IsValid)
        {
            throw new ValidationException(typeValidation.ErrorMessage!);
        }

        _ = AdvancedSearchValidator.TryParseType(type, out var contentType);

        return new DiscoveryCriteria(
            contentType,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);
    }

    private static DiscoverBrowseCriteria BuildBrowseCriteria(
        string mode,
        string? type,
        int? page,
        int? pageSize,
        string[]? genreId,
        int? year,
        decimal? minRating,
        string? language,
        string? sort)
    {
        var modeValidation = DiscoverBrowseValidator.ValidateMode(mode);
        if (!modeValidation.IsValid)
        {
            throw new ValidationException(modeValidation.ErrorMessage!);
        }

        var typeValidation = AdvancedSearchValidator.ValidateType(type);
        if (!typeValidation.IsValid)
        {
            throw new ValidationException(typeValidation.ErrorMessage!);
        }

        var sortValidation = DiscoverBrowseValidator.ValidateSort(sort);
        if (!sortValidation.IsValid)
        {
            throw new ValidationException(sortValidation.ErrorMessage!);
        }

        _ = DiscoverBrowseValidator.TryParseMode(mode, out var browseMode);
        _ = AdvancedSearchValidator.TryParseType(type, out var contentType);
        _ = DiscoverBrowseValidator.TryParseSort(sort, out var browseSort);

        var criteria = new DiscoverBrowseCriteria(
            browseMode,
            contentType,
            DiscoverBrowseValidator.ParseGenreIds(genreId, null),
            year,
            minRating,
            language,
            browseSort,
            page ?? SearchPaginationDefaults.DefaultPage,
            pageSize ?? SearchPaginationDefaults.DefaultPageSize);

        var validation = DiscoverBrowseValidator.Validate(criteria);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        return criteria;
    }

    private static ProblemDetails CreateProblemDetails(int statusCode, string title, string detail) =>
        new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
}
