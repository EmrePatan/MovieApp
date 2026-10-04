namespace MovieApp.Application.Services.Discovery;

/// <summary>
/// Bu Hafta TV'de keeps TV titles from tv/on_the_air and drops items with no poster
/// or almost no votes. Talk / News / Reality shaping is handled by
/// <see cref="OnTvThisWeekContentSelector"/>. TMDB on_the_air does not accept a region.
/// </summary>
public static class OnTvThisWeekQualityFilter
{
    public const int MinimumVoteCount = 40;

    public static bool Include(int voteCount, string? posterPath) =>
        voteCount >= MinimumVoteCount && !string.IsNullOrWhiteSpace(posterPath);
}
