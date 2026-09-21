namespace MovieApp.Application.ProductMetrics;

public static class ProductMetricNames
{
    public const string DiscoverOpened = "discover_opened";
    public const string AdvancedDiscoverOpened = "advanced_discover_opened";
    public const string StreamingServicesOpened = "streaming_services_opened";
    public const string NowInTheatersOpened = "now_in_theaters_opened";
    public const string OnTvThisWeekOpened = "on_tv_this_week_opened";
    public const string WorldCinemaOpened = "world_cinema_opened";
    public const string ContentDetailOpened = "content_detail_opened";
    public const string PickSomethingOpened = "pick_something_opened";
    public const string PickSomethingGenerated = "pick_something_generated";
    public const string AiRecommendationsOpened = "ai_recommendations_opened";
    public const string AiRecommendationsGenerated = "ai_recommendations_generated";
    public const string SearchSubmitted = "search_submitted";
    public const string WatchlistCreated = "watchlist_created";
    public const string ReviewCreated = "review_created";
    public const string RatingCreated = "rating_created";

    public const string LibraryOpened = "library_opened";
    public const string LibraryFilterSelected = "library_filter_selected";
    public const string InsightsOpened = "insights_opened";

    /// <summary>Legacy mobile metric retained for backward-compatible increments.</summary>
    public const string PickSomethingUsed = "pick_something_used";

    /// <summary>Legacy mobile metric retained for backward-compatible increments.</summary>
    public const string AiRecommendationsUsed = "ai_recommendations_used";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        DiscoverOpened,
        AdvancedDiscoverOpened,
        StreamingServicesOpened,
        NowInTheatersOpened,
        OnTvThisWeekOpened,
        WorldCinemaOpened,
        ContentDetailOpened,
        PickSomethingOpened,
        PickSomethingGenerated,
        AiRecommendationsOpened,
        AiRecommendationsGenerated,
        SearchSubmitted,
        WatchlistCreated,
        ReviewCreated,
        RatingCreated,
        LibraryOpened,
        LibraryFilterSelected,
        InsightsOpened,
        PickSomethingUsed,
        AiRecommendationsUsed,
    };

    public static bool IsAllowed(string? metricName) =>
        !string.IsNullOrWhiteSpace(metricName) && Allowed.Contains(metricName.Trim());
}
