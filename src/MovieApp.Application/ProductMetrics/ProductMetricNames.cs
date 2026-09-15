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
    public const string PickSomethingUsed = "pick_something_used";

    public const string LibraryOpened = "library_opened";
    public const string LibraryFilterSelected = "library_filter_selected";

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        DiscoverOpened,
        AdvancedDiscoverOpened,
        StreamingServicesOpened,
        NowInTheatersOpened,
        OnTvThisWeekOpened,
        WorldCinemaOpened,
        ContentDetailOpened,
        PickSomethingUsed,
        LibraryOpened,
        LibraryFilterSelected,
    };

    public static bool IsAllowed(string? metricName) =>
        !string.IsNullOrWhiteSpace(metricName) && Allowed.Contains(metricName.Trim());
}
