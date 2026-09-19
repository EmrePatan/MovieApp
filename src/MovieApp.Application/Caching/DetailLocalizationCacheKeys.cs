namespace MovieApp.Application.Caching;

public static class DetailLocalizationCacheKeys
{
    private const string Version = "v1";

    public static string Movie(int tmdbId, string contentLocale) =>
        $"movie-detail-loc:{tmdbId}:{NormalizeLocale(contentLocale)}:{Version}";

    public static string TvShow(int tmdbId, string contentLocale) =>
        $"tvshow-detail-loc:{tmdbId}:{NormalizeLocale(contentLocale)}:{Version}";

    public static string Person(int tmdbPersonId, string contentLocale) =>
        $"person-detail-loc:{tmdbPersonId}:{NormalizeLocale(contentLocale)}:{Version}";

    public static string Collection(int tmdbCollectionId, string contentLocale) =>
        $"collection-detail-loc:{tmdbCollectionId}:{NormalizeLocale(contentLocale)}:{Version}";

    private static string NormalizeLocale(string contentLocale) =>
        contentLocale.Trim().ToLowerInvariant();
}
