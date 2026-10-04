namespace MovieApp.Application.Models.Discovery;

/// <summary>
/// Selects On TV This Week content policy for Home vs Discover See All (separate cache and entry points).
/// </summary>
public enum OnTvThisWeekPresentationIntent
{
    DiscoverBrowse = 0,
    HomeRail = 1,
}
