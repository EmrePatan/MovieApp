namespace MovieApp.Application.Services.StreamingLeaving;

/// <summary>
/// Maps streaming availability service ids to TMDB watch provider ids, which is what saved
/// user platforms use. TMDB uses different ids per region and plan, so a service lists all of them.
/// A service missing here is still shown; it only never matches the "my platforms" filter.
/// </summary>
public static class StreamingPlatformMap
{
    private static readonly Dictionary<string, int[]> TmdbProviderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["netflix"] = [8, 1796],
        ["prime"] = [9, 119, 2100],
        ["disney"] = [337],
        ["hbo"] = [384, 1899],
        ["hulu"] = [15],
        ["apple"] = [350],
        ["paramount"] = [531, 2303, 2304, 2616],
        ["peacock"] = [386, 387],
        ["starz"] = [43],
        ["mubi"] = [11],
        ["crunchyroll"] = [283],
        ["curiosity"] = [190],
        ["discovery"] = [520, 524],
        ["britbox"] = [151],
        ["criterion"] = [258],
        ["amc"] = [526],
        ["zee5"] = [232]
    };

    public static bool Matches(string serviceId, IReadOnlyCollection<int> watchProviderIds) =>
        TmdbProviderIds.TryGetValue(serviceId, out var ids) && ids.Any(watchProviderIds.Contains);
}
