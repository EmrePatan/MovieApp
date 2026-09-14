namespace MovieApp.Application.Services.TvShowChanges;

public static class TmdbTvChangesWindowPlanner
{
    public const int MaxWindowDays = 14;

    public const int OverlapDays = 1;

    /// <summary>
    /// Initial catch-up window when no checkpoint exists: last two UTC calendar days ending at the target date.
    /// </summary>
    public const int InitialLookbackDays = 2;

    public static DateOnly DetermineTargetDate(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow);

    public static DateOnly? DetermineNextWindowStart(DateOnly? lastCompletedEndDate, DateOnly targetDate)
    {
        if (lastCompletedEndDate is null)
        {
            return targetDate.AddDays(-(InitialLookbackDays - 1));
        }

        return lastCompletedEndDate.Value.AddDays(-(OverlapDays - 1));
    }

    public static IReadOnlyList<(DateOnly Start, DateOnly End)> BuildChunks(DateOnly windowStart, DateOnly windowEnd)
    {
        if (windowEnd < windowStart)
        {
            return [];
        }

        var chunks = new List<(DateOnly Start, DateOnly End)>();
        var cursor = windowStart;

        while (cursor <= windowEnd)
        {
            var chunkEnd = cursor.AddDays(MaxWindowDays - 1);
            if (chunkEnd > windowEnd)
            {
                chunkEnd = windowEnd;
            }

            chunks.Add((cursor, chunkEnd));
            cursor = chunkEnd.AddDays(1);
        }

        return chunks;
    }
}
