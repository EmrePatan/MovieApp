using MovieApp.Application.Models.Discovery;

namespace MovieApp.Application.Caching;

public static class NowInTheatersCacheKeys
{
    public static string Create(NowInTheatersCriteria criteria) =>
        string.Join(
            ':',
            "discovery-now-in-theaters",
            criteria.ReleaseRegion.Trim().ToUpperInvariant(),
            criteria.Page,
            criteria.PageSize,
            "v1");
}
