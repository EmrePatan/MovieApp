using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IUnifiedSearchProviderIngestionService
{
    Task<UnifiedSearchProviderIngestionResult> IngestAsync(
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchSuggestion>> GetAutocompleteSuggestionsAsync(
        string query,
        int limit,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
