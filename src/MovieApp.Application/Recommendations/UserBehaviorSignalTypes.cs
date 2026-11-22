namespace MovieApp.Application.Recommendations;

public static class UserBehaviorSignalTypes
{
    public const string Rating = "rating";

    public const string Favorite = "favorite";

    public const string Watched = "watched";

    public const string Watchlist = "watchlist";

    public const string Search = "search";

    public const string TvFollow = "tv-follow";

    /// <summary>
    /// Explicit "not interested" action from a recommendation surface. Carries a negative taste weight.
    /// </summary>
    public const string Dismissed = "dismissed";
}
