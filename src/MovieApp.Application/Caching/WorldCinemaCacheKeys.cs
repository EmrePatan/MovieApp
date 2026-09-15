using MovieApp.Application.Models.Discovery;

namespace MovieApp.Application.Caching;

public static class WorldCinemaCacheKeys
{
    public static string Create(WorldCinemaCriteria criteria) =>
        string.Join(
            ':',
            "discovery-world-cinema",
            criteria.MediaType.ToString().ToLowerInvariant(),
            criteria.OriginCountry.Trim().ToUpperInvariant(),
            criteria.Sort.ToString(),
            criteria.Page,
            criteria.PageSize,
            "v2");
}
