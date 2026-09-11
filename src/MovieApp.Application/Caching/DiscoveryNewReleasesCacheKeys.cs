using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryNewReleasesCacheKeys
{
    public const string Prefix = "discovery-new-releases:";

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{criteria.Type}:{criteria.Page}:{criteria.PageSize}";
}
