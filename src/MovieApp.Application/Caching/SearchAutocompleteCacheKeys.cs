namespace MovieApp.Application.Caching;

public static class SearchAutocompleteCacheKeys
{
    public const string Prefix = "search-autocomplete:";

    public static string Create(string query) =>
        $"{Prefix}{Common.QueryNormalizer.Normalize(query)}";
}
