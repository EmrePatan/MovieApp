using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryGenreCacheKeys
{
    public const string Prefix = "discovery-genre:";

    public static string Create(string genreName, DiscoveryCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(genreName, criteria), contentLocale);

    public static string Create(string genreName, DiscoveryCriteria criteria) =>
        $"{Prefix}{genreName}:{criteria.Type}:{criteria.Page}:{criteria.PageSize}";
}
