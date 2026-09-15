namespace MovieApp.Application.Caching;

public static class CollectionCacheKeys
{
    public const string Prefix = "collection:";

    public const string Version = "v1";

    public static string Create(int tmdbCollectionId) =>
        $"{Prefix}{tmdbCollectionId}:{Version}";
}
