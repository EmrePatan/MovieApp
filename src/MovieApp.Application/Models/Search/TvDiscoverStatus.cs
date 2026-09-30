namespace MovieApp.Application.Models.Search;

/// <summary>
/// TMDB discover/tv <c>with_status</c> integer values.
/// </summary>
public enum TvDiscoverStatus
{
    ReturningSeries = 0,
    Planned = 1,
    InProduction = 2,
    Ended = 3,
    Canceled = 4,
    Pilot = 5,
}
