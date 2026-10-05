namespace MovieApp.Infrastructure.Persistence.Catalog;

internal static class CatalogChangesRelevanceTmdbIdBatching
{
    internal const int BatchSize = 500;

    internal static IEnumerable<int[]> EnumerateBatches(IReadOnlyCollection<int> tmdbIds)
    {
        if (tmdbIds.Count == 0)
        {
            yield break;
        }

        var buffer = new int[BatchSize];
        var count = 0;

        foreach (var tmdbId in tmdbIds)
        {
            buffer[count++] = tmdbId;
            if (count == BatchSize)
            {
                yield return buffer;
                buffer = new int[BatchSize];
                count = 0;
            }
        }

        if (count > 0)
        {
            Array.Resize(ref buffer, count);
            yield return buffer;
        }
    }
}
