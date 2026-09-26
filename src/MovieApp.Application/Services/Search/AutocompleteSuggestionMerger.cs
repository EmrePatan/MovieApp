using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class AutocompleteSuggestionMerger
{
    public static IReadOnlyList<SearchSuggestion> Merge(
        IReadOnlyList<SearchSuggestion> providerSuggestions,
        IReadOnlyList<SearchSuggestion> localSuggestions,
        int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        var seen = new HashSet<Guid>();
        var merged = new List<SearchSuggestion>(Math.Min(limit, providerSuggestions.Count + localSuggestions.Count));

        AppendUnique(providerSuggestions, seen, merged, limit);
        if (merged.Count < limit)
        {
            AppendUnique(localSuggestions, seen, merged, limit);
        }

        return merged;
    }

    private static void AppendUnique(
        IReadOnlyList<SearchSuggestion> source,
        HashSet<Guid> seen,
        List<SearchSuggestion> destination,
        int limit)
    {
        foreach (var suggestion in source)
        {
            if (!seen.Add(suggestion.Id))
            {
                continue;
            }

            destination.Add(suggestion);
            if (destination.Count >= limit)
            {
                return;
            }
        }
    }
}
