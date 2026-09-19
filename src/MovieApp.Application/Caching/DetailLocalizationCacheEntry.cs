namespace MovieApp.Application.Caching;

internal sealed class DetailLocalizationCacheEntry<T>
    where T : class
{
    public T Data { get; init; } = null!;
}
