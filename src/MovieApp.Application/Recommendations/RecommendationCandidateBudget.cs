namespace MovieApp.Application.Recommendations;

public static class RecommendationCandidateBudget
{
    public static (int MovieBudget, int TvBudget) Split(int maxCandidates, int minorityFloor)
    {
        if (maxCandidates <= 0)
        {
            return (0, 0);
        }

        if (maxCandidates == 1)
        {
            return (1, 0);
        }

        var half = maxCandidates / 2;
        var floor = minorityFloor <= 0 ? half : Math.Min(minorityFloor, half);
        var movieBudget = Math.Max(floor, maxCandidates - half);
        var tvBudget = maxCandidates - movieBudget;
        if (tvBudget < floor)
        {
            tvBudget = floor;
            movieBudget = maxCandidates - tvBudget;
        }

        return (movieBudget, tvBudget);
    }

    public static List<Guid> RoundRobin(IReadOnlyList<IReadOnlyList<Guid>> buckets, int maxCandidates)
    {
        var selected = new List<Guid>();
        if (maxCandidates <= 0 || buckets.Count == 0)
        {
            return selected;
        }

        var seen = new HashSet<Guid>();
        var index = 0;
        var added = true;
        while (selected.Count < maxCandidates && added)
        {
            added = false;
            foreach (var bucket in buckets)
            {
                if (index >= bucket.Count)
                {
                    continue;
                }

                added = true;
                if (seen.Add(bucket[index]))
                {
                    selected.Add(bucket[index]);
                    if (selected.Count >= maxCandidates)
                    {
                        return selected;
                    }
                }
            }

            index++;
        }

        return selected;
    }

    public static List<Guid> AppendDistinct(List<Guid> selected, IReadOnlyList<Guid> more, int maxCandidates)
    {
        if (selected.Count >= maxCandidates)
        {
            return selected;
        }

        var seen = selected.ToHashSet();
        foreach (var id in more)
        {
            if (!seen.Add(id))
            {
                continue;
            }

            selected.Add(id);
            if (selected.Count >= maxCandidates)
            {
                break;
            }
        }

        return selected;
    }
}
