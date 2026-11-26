namespace MovieApp.Application.Models.Search;

/// <summary>Subscription streaming filter for browse-style lists: any of the providers in the region.</summary>
public sealed record WatchProviderFilter(string Region, IReadOnlyList<int> ProviderIds)
{
    public string CacheSegment => $"watch-{Region}-{string.Join('-', ProviderIds.OrderBy(id => id))}";
}
