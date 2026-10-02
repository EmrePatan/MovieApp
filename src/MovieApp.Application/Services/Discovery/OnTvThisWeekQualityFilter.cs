namespace MovieApp.Application.Services.Discovery;

/// <summary>
/// Bu Hafta TV'de keeps TV titles from tv/on_the_air and drops talk-show junk
/// that has no poster or almost no votes. TMDB on_the_air does not accept a region.
/// </summary>
public static class OnTvThisWeekQualityFilter
{
    public const int MinimumVoteCount = 40;

    public static bool Include(int voteCount, string? posterPath) =>
        voteCount >= MinimumVoteCount && !string.IsNullOrWhiteSpace(posterPath);
}
