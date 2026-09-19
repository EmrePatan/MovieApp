namespace MovieApp.Application.Caching;

public static class SearchAutocompleteCacheKeys
{
    public const string Prefix = "search-autocomplete:v2:";

    public static string Create(string query, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(query), contentLocale);

    public static string Create(string query) =>
        $"{Prefix}{Common.QueryNormalizer.Normalize(query)}";
}
