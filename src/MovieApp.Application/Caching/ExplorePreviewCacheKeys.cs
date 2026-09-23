namespace MovieApp.Application.Caching;

public static class ExplorePreviewCacheKeys
{
    public const string Prefix = "discovery-explore-preview:";

    public const string Version = "v1";

    public static string Create(int sectionSize, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append($"{Prefix}{sectionSize}:{Version}", contentLocale);
}
