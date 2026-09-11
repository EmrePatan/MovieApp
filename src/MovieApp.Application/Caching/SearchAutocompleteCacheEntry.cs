using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public sealed class SearchAutocompleteCacheEntry
{
    public required IReadOnlyList<SearchSuggestion> Items { get; init; }
}
