namespace MovieApp.Application.Caching;

public static class SearchRefreshCompletionKeys
{
    public const string Prefix = "search-refresh-completion:";

    public static string FromLockKey(string lockKey) =>
        lockKey.Replace(
            SearchRefreshLockKeys.Prefix.TrimEnd(':'),
            Prefix.TrimEnd(':'),
            StringComparison.Ordinal);
}
