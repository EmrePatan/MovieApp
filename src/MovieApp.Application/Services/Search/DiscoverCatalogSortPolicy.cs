using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Shared server-side discover sort constraints (vote floors, release upper bounds).
/// </summary>
public static class DiscoverCatalogSortPolicy
{
    /// <summary>
    /// Established browse Top Rated mode floor (see <c>TmdbDiscoverQueryBuilder.TopRatedMinimumVoteCount</c>).
    /// </summary>
    public const int TopRatedMinimumVoteCount = 50;

    /// <summary>
    /// Centralized future-release cap: UTC today + horizon days (product fallback).
    /// </summary>
    public const int FutureReleaseHorizonDays = 90;

    public static DateOnly GetFutureReleaseCutoffDate(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow).AddDays(FutureReleaseHorizonDays);

    public static bool RequiresFutureReleaseCap(DiscoverBrowseSort sort) =>
        sort == DiscoverBrowseSort.ReleaseDesc;

    public static bool RequiresFutureReleaseCap(AdvancedDiscoverSort sort) =>
        sort == AdvancedDiscoverSort.Newest;

    public static bool RequiresRatingVoteFloor(DiscoverBrowseSort sort) =>
        sort == DiscoverBrowseSort.RatingDesc;

    public static bool RequiresRatingVoteFloor(AdvancedDiscoverSort sort) =>
        sort == AdvancedDiscoverSort.RatingDesc;

    public static int? ResolveBrowseMinVoteCount(
        DiscoverBrowseMode mode,
        DiscoverBrowseSort effectiveSort,
        int? requestedMinVoteCount)
    {
        int? floor = null;

        if (mode == DiscoverBrowseMode.TopRated)
        {
            floor = TopRatedMinimumVoteCount;
        }

        if (RequiresRatingVoteFloor(effectiveSort))
        {
            floor = floor.HasValue
                ? Math.Max(floor.Value, TopRatedMinimumVoteCount)
                : TopRatedMinimumVoteCount;
        }

        if (requestedMinVoteCount is null)
        {
            return floor;
        }

        if (floor is null)
        {
            return requestedMinVoteCount;
        }

        return Math.Max(requestedMinVoteCount.Value, floor.Value);
    }

    public static int? ResolveAdvancedMinVoteCount(
        AdvancedDiscoverSort sort,
        int? requestedMinVoteCount,
        int? watchProviderFloor)
    {
        int? floor = watchProviderFloor;

        if (RequiresRatingVoteFloor(sort))
        {
            floor = floor.HasValue
                ? Math.Max(floor.Value, TopRatedMinimumVoteCount)
                : TopRatedMinimumVoteCount;
        }

        if (requestedMinVoteCount is null)
        {
            return floor;
        }

        if (floor is null)
        {
            return requestedMinVoteCount;
        }

        return Math.Max(requestedMinVoteCount.Value, floor.Value);
    }

    public static DateOnly? ResolveReleaseUpperBound(
        DateOnly? userUpper,
        DateOnly? futureCap,
        DateOnly? newReleasesTodayCap)
    {
        DateOnly? bound = userUpper;

        if (newReleasesTodayCap.HasValue)
        {
            bound = bound.HasValue
                ? (bound.Value < newReleasesTodayCap.Value ? bound.Value : newReleasesTodayCap.Value)
                : newReleasesTodayCap.Value;
        }

        if (futureCap.HasValue)
        {
            bound = bound.HasValue
                ? (bound.Value < futureCap.Value ? bound.Value : futureCap.Value)
                : futureCap.Value;
        }

        return bound;
    }
}
