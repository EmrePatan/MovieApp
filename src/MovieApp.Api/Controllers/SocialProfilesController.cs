using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Localization;
using MovieApp.Api.Mapping;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Common;
using MovieApp.Application.Services.Insights;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Validation;
using MovieApp.Contracts.Users;
using MovieApp.Contracts.Watchlists;

namespace MovieApp.Api.Controllers;

public sealed record SetSocialProfileCoverPosterRequest(string? PosterPath);

[ApiController]
[Route("api/social/profiles")]
public sealed class SocialProfilesController(
    ISocialProfileService socialProfileService,
    ISocialFeedService socialFeedService,
    IInsightsV3Service insightsV3Service) : ControllerBase
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
        try
        {
            return Ok(await socialFeedService.GetUserActivitiesAsync(
                userId,
                cursor,
                pageSize,
                Request.ResolveContentLocale(),
                cancellationToken));
        }
        catch (AuthenticationException exception) { return Unauthorized(new ProblemDetails { Detail = exception.Message }); }
        catch (NotFoundException) { return NotFound(new ProblemDetails { Title = "User not found." }); }
        catch (ValidationException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/statistics")]
    [ProducesResponseType(typeof(SocialProfileStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SocialProfileStatisticsResponse>> Statistics(
        Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await socialProfileService.GetStatisticsAsync(userId, cancellationToken);
            var contentLocale = Request.ResolveContentLocale();
            return Ok(new SocialProfileStatisticsResponse(
                statistics.MoviesWatched,
                statistics.EpisodesWatched,
                statistics.RatingsCount,
                statistics.ReviewsCount,
                statistics.FavoritesCount,
                statistics.Milestones
                    .Select(milestone => new SocialProfileMilestoneResponse(
                        milestone.Id,
                        AchievementMilestoneLocalization.GetTitle(milestone.Id, contentLocale, milestone.Title),
                        AchievementMilestoneLocalization.GetDescription(milestone.Id, contentLocale, milestone.Description),
                        milestone.AchievedAt))
                    .ToList(),
                statistics.MovieRuntimeMinutes,
                statistics.EpisodeRuntimeMinutes,
                statistics.FavoriteMovies
                    .Select(MapShelfItem)
                    .ToList(),
                statistics.FavoriteShows
                    .Select(MapShelfItem)
                    .ToList(),
                statistics.LibraryTitles
                    .Select(MapShelfItem)
                    .ToList(),
                statistics.WatchingTitles
                    .Select(MapShelfItem)
                    .ToList(),
                statistics.WatchedTitles
                    .Select(MapShelfItem)
                    .ToList(),
                statistics.LikedTitles
                    .Select(MapShelfItem)
                    .ToList(),
                statistics.WatchlistTitles
                    .Select(MapShelfItem)
                    .ToList()));
        }
        catch (NotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "User not found." });
        }
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

            // The level is a summary of watched/rated activity, so it follows the same
            // visibility rule as the profile's other activity.
            int? achievementPoints = null;
            IReadOnlyList<PublicRecentAchievementResponse>? recentAchievements = null;
            if (await socialProfileService.CanViewReviewsAsync(userId, cancellationToken))
            {
                var insights = await insightsV3Service.GetInsightsV3ForUserAsync(userId, "UTC", null, cancellationToken);
                achievementPoints = AchievementPoints.Sum(insights.Achievements);
                recentAchievements = insights.Achievements
                    .Where(item => item.Achieved && item.AchievedAt.HasValue)
                    .OrderByDescending(item => item.AchievedAt)
                    .ThenByDescending(item => item.Tier)
                    .Take(5)
                    .Select(item => new PublicRecentAchievementResponse(
                        item.Id,
                        item.Category,
                        item.Tier,
                        item.TargetValue,
                        item.AchievedAt!.Value))
                    .ToList();
            }

            return Ok(new PublicSocialProfileResponse(
                profile.Id,
                profile.UserName,
                profile.DisplayName,
                profile.CreatedAt,
                profile.AvatarUrl,
                profile.IsPrivate,
                profile.FollowStatus,
                profile.FollowersCount,
                profile.FollowingCount,
                profile.CoverPosterPath,
                profile.CoverImageUrl,
                profile.Bio,
                UserProfileContractMapper.ToSocialLinksDto(profile.SocialLinks),
                profile.FollowsViewer,
                achievementPoints,
                recentAchievements));
        }
        catch (NotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "User not found." });
        }
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/watchlists")]
    [ProducesResponseType(typeof(IReadOnlyList<WatchlistSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<WatchlistSummaryResponse>>> GetWatchlists(
        Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var results = await socialProfileService.GetWatchlistsAsync(userId, cancellationToken);
            return Ok(results.Select(WatchlistContractMapper.ToSummaryResponse).ToList());
        }
        catch (NotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "User not found." });
        }
    }

    [AllowAnonymous]
    [HttpGet("{userId:guid}/watchlists/{watchlistId:guid}/items")]
    [ProducesResponseType(typeof(WatchlistItemsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WatchlistItemsResponse>> GetWatchlistItems(
        Guid userId,
        Guid watchlistId,
        [FromQuery] string? mediaType,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var mediaTypeValidation = WatchlistItemsValidator.ValidateMediaType(mediaType);
        if (!mediaTypeValidation.IsValid)
        {
            return BadRequest(new ProblemDetails { Detail = mediaTypeValidation.ErrorMessage });
        }

        var sortValidation = WatchlistItemsValidator.ValidateSort(sort);
        if (!sortValidation.IsValid)
        {
            return BadRequest(new ProblemDetails { Detail = sortValidation.ErrorMessage });
        }

        _ = AdvancedSearchValidator.TryParseType(mediaType, out var parsedMediaType);
        _ = WatchlistItemsValidator.TryParseSort(sort, out var parsedSort);

        try
        {
            var result = await socialProfileService.GetWatchlistItemsAsync(
                userId,
                watchlistId,
                parsedMediaType,
                parsedSort,
                page ?? SearchPaginationDefaults.DefaultPage,
                pageSize ?? SearchPaginationDefaults.DefaultPageSize,
                cancellationToken);

            return Ok(WatchlistContractMapper.ToItemsResponse(result));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message });
        }
        catch (NotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "Watchlist not found." });
        }
    }

    [Authorize]
    [HttpPatch("me/cover-poster")]
    public async Task<IActionResult> SetCoverPoster(
        [FromBody] SetSocialProfileCoverPosterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await socialProfileService.SetProfileCoverPosterAsync(request.PosterPath, cancellationToken);
            return NoContent();
        }
        catch (AuthenticationException exception)
        {
            return Unauthorized(new ProblemDetails { Detail = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new ProblemDetails { Detail = exception.Message });
        }
        catch (NotFoundException)
        {
            return NotFound(new ProblemDetails { Title = "User not found." });
        }
    }

    private static SocialProfileShelfItemResponse MapShelfItem(SocialProfileShelfItemResult item) =>
        new(item.Id, item.Title, item.PosterPath, item.ContentType, item.ProgressPercentage);
}
