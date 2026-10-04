using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

/// <summary>
/// Weekly trending (TMDB <c>trending/all/week</c>) is a momentum signal; unreleased titles belong in Coming Soon.
/// </summary>
public static class TrendingWeekReleaseEligibility
{
    public static DateOnly TodayUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Movies use <paramref name="releaseOrFirstAirDate"/> as release date; TV uses first air date.
    /// Missing dates remain eligible (same as catalog items without a known future date).
    /// </summary>
    public static bool IsReleased(DateOnly? releaseOrFirstAirDate, DateOnly today) =>
        releaseOrFirstAirDate is null || releaseOrFirstAirDate.Value <= today;

    public static List<SearchItem> FilterReleased(
        IReadOnlyList<SearchItem> itemsInProviderOrder,
        DateOnly today)
    {
        var result = new List<SearchItem>(itemsInProviderOrder.Count);
        foreach (var item in itemsInProviderOrder)
        {
            if (IsReleased(item.ReleaseDate, today))
            {
                result.Add(item);
            }
        }

        return result;
    }

    public static List<TrendingWeekProviderItem> FilterReleased(
        IReadOnlyList<TrendingWeekProviderItem> itemsInProviderOrder,
        DateOnly today)
    {
        var result = new List<TrendingWeekProviderItem>(itemsInProviderOrder.Count);
        foreach (var item in itemsInProviderOrder)
        {
            if (IsReleased(item.ReleaseDate, today))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
