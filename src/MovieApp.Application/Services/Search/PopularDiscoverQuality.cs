namespace MovieApp.Application.Services.Search;

/// <summary>
/// Popüler uses TMDB discover popularity order. These floors drop junk before that order is kept.
/// </summary>
public static class PopularDiscoverQuality
{
    public const int MinimumVoteCountMovie = 50;

    public const int MinimumVoteCountTv = 20;
}
