using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Shared server-side discover sort constraints (vote floors, release upper bounds).
/// </summary>
public static class DiscoverCatalogSortPolicy
{
    /// <summary>
    /// Curated Top Rated / Highest Rated list floor (home shelf, browse mode, catalog top-rated query).
    /// </summary>
    public const int CuratedTopRatedListMinimumVoteCount = 1000;

    /// <summary>
    /// Rating-ranked sort floor for non-curated browse modes (e.g. user sorts Trending by rating).
    /// </summary>
    public const int RatingRankedSortMinimumVoteCount = 50;

    /// <summary>
    /// Browse <see cref="DiscoverBrowseMode.TopRated"/> TMDB discover floor.
    /// </summary>
    public const int TopRatedMinimumVoteCount = CuratedTopRatedListMinimumVoteCount;
    public const int TopRatedMinimumVoteCountTv = 500;

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
        int? requestedMinVoteCount,
        int? newReleasesVoteFloor = null,
        int topRatedVoteFloor = CuratedTopRatedListMinimumVoteCount)
    {
        int? floor = null;

        if (mode == DiscoverBrowseMode.TopRated)
        {
            floor = topRatedVoteFloor;
        }
        else if (RequiresRatingVoteFloor(effectiveSort))
        {
            floor = RatingRankedSortMinimumVoteCount;
        }

        if (mode == DiscoverBrowseMode.NewReleases && newReleasesVoteFloor is > 0)
        {
            floor = floor.HasValue
                ? Math.Max(floor.Value, newReleasesVoteFloor.Value)
                : newReleasesVoteFloor.Value;
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
                ? Math.Max(floor.Value, RatingRankedSortMinimumVoteCount)
                : RatingRankedSortMinimumVoteCount;
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
