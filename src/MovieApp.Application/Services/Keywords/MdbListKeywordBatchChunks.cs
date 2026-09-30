namespace MovieApp.Application.Services.Keywords;

internal static class MdbListKeywordBatchChunks
{
    public const int MaxTmdbIdsPerRequest = 200;

    public static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> items, int chunkSize)
    {
        if (items.Count == 0)
        {
            yield break;
        }

        var size = Math.Clamp(chunkSize, 1, MaxTmdbIdsPerRequest);
        for (var index = 0; index < items.Count; index += size)
        {
            var count = Math.Min(size, items.Count - index);
            var chunk = new List<T>(count);
            for (var offset = 0; offset < count; offset++)
            {
                chunk.Add(items[index + offset]);
            }

            yield return chunk;
        }
    }
}
