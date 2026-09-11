namespace MovieApp.Contracts.Search;

public sealed record SearchAutocompleteResponse(IReadOnlyList<SearchAutocompleteItemResponse> Items);
