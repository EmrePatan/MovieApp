using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Mapping;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Contracts.Credits;
using MovieApp.Contracts.TvShows;
using MovieApp.Contracts.WatchProviders;

namespace MovieApp.Api.Controllers;

[ApiController]
[Route("api/tvshows")]
public sealed class TvShowsController(
    ISearchTvShowsService searchTvShowsService,
    IGetTvShowByIdService getTvShowByIdService,
    IGetSeasonService getSeasonService,
    IGetEpisodeService getEpisodeService,
    IGetTvShowCreditsService getTvShowCreditsService,
    IGetTvShowWatchProvidersService getTvShowWatchProvidersService) : ControllerBase
{
    [HttpGet("search")]
    [EnableRateLimiting(SearchRateLimitPolicies.TvSearch)]
    [ProducesResponseType(typeof(TvShowSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TvShowSearchResponse>> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new TvShowSearchRequest(
                query ?? string.Empty,
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize);

            var results = await searchTvShowsService.SearchAsync(request, cancellationToken);
            return Ok(TvShowContractMapper.ToSearchResponse(results));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid search request.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TvShowDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TvShowDetailsResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var tvShow = await getTvShowByIdService.GetByIdAsync(id, cancellationToken);
            return Ok(TvShowContractMapper.ToDetailsResponse(tvShow));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "TV show not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/credits")]
    [ProducesResponseType(typeof(CreditsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreditsResponse>> GetCredits(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var credits = await getTvShowCreditsService.GetCreditsAsync(id, cancellationToken);
            return Ok(CreditsContractMapper.ToResponse(credits));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "TV show not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/watch-providers")]
    [ProducesResponseType(typeof(WatchProvidersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchProvidersResponse>> GetWatchProviders(
        Guid id,
        [FromQuery] string? region,
        CancellationToken cancellationToken)
    {
        try
        {
            var providers = await getTvShowWatchProvidersService.GetWatchProvidersAsync(id, region, cancellationToken);
            return Ok(WatchProvidersContractMapper.ToResponse(providers));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid watch provider request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "TV show not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/seasons/{seasonNumber:int}")]
    [ProducesResponseType(typeof(SeasonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeasonResponse>> GetSeason(
        Guid id,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var season = await getSeasonService.GetSeasonAsync(id, seasonNumber, cancellationToken);
            return Ok(TvShowContractMapper.ToSeasonResponse(season));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid season request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Season not found.",
                exception.Message));
        }
    }

    [HttpGet("{id:guid}/seasons/{seasonNumber:int}/episodes/{episodeNumber:int}")]
    [ProducesResponseType(typeof(EpisodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EpisodeResponse>> GetEpisode(
        Guid id,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var episode = await getEpisodeService.GetEpisodeAsync(
                id,
                seasonNumber,
                episodeNumber,
                cancellationToken);

            return Ok(TvShowContractMapper.ToEpisodeResponse(episode));
        }
        catch (ValidationException exception)
        {
            return BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Invalid episode request.",
                exception.Message));
        }
        catch (NotFoundException exception)
        {
            return NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Episode not found.",
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
