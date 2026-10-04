using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Discovery;

/// <summary>
/// World Cinema vote-count guardrails by sort mode (not Top Rated catalog thresholds).
/// </summary>
public static class WorldCinemaDiscoverQuality
{
    public const int PopularMinimumVoteCount = 50;

    public const int NewestMinimumVoteCount = 1;

    public const int RatingMinimumVoteCountMovie = 100;

    public const int RatingMinimumVoteCountTv = 75;

    public static int ResolveMinimumVoteCount(SearchContentType mediaType, AdvancedDiscoverSort sort) =>
        sort switch
        {
            AdvancedDiscoverSort.RatingDesc => mediaType switch
            {
                SearchContentType.Tv => RatingMinimumVoteCountTv,
                _ => RatingMinimumVoteCountMovie
            },
            AdvancedDiscoverSort.Newest => NewestMinimumVoteCount,
            _ => PopularMinimumVoteCount
        };
}
