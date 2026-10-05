namespace MovieApp.Application.Models.Changes;

public sealed class CatalogChangesRefreshMaps
{
    public required IReadOnlyDictionary<int, Guid> UserRelevantByTmdbId { get; init; }

    public required IReadOnlyDictionary<int, Guid> DiscoveryRelevantByTmdbId { get; init; }

    public required IReadOnlyDictionary<int, Guid> UnionByTmdbId { get; init; }

    public static CatalogChangesRefreshMaps Create(
        IReadOnlyDictionary<int, Guid> userRelevant,
        IReadOnlyDictionary<int, Guid> discoveryRelevant)
    {
        var union = new Dictionary<int, Guid>(userRelevant);
        foreach (var (tmdbId, catalogId) in discoveryRelevant)
        {
            union.TryAdd(tmdbId, catalogId);
        }

        return new CatalogChangesRefreshMaps
        {
            UserRelevantByTmdbId = userRelevant,
            DiscoveryRelevantByTmdbId = discoveryRelevant,
            UnionByTmdbId = union
        };
    }
}
