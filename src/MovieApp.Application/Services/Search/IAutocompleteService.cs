using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IAutocompleteService
{
    Task<IReadOnlyList<SearchSuggestion>> GetSuggestionsAsync(
        string query,
        CancellationToken cancellationToken = default);
}
