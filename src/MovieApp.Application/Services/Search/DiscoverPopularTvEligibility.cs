using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// TV genre eligibility for Discover Popular browse (rail and See All share this policy).
/// </summary>
public static class DiscoverPopularTvEligibility
{
    public static bool IsEligibleForPopular(DiscoverBrowseMode mode, TvShowProviderSummary summary) =>
        mode != DiscoverBrowseMode.Popular || !ContainsTalkGenre(summary.GenreTmdbIds);

    public static IReadOnlyList<TvShowProviderSummary> FilterEligible(
        DiscoverBrowseMode mode,
        IReadOnlyList<TvShowProviderSummary> summaries)
    {
        if (mode != DiscoverBrowseMode.Popular)
        {
            return summaries;
        }

        var eligible = new List<TvShowProviderSummary>(summaries.Count);
        foreach (var summary in summaries)
        {
            if (!ContainsTalkGenre(summary.GenreTmdbIds))
            {
                eligible.Add(summary);
            }
        }

        return eligible;
    }

    private static bool ContainsTalkGenre(IReadOnlyList<int>? genreTmdbIds) =>
        genreTmdbIds is not null &&
        genreTmdbIds.Contains(OnTvThisWeekContentSelector.TalkTmdbGenreId);
}
