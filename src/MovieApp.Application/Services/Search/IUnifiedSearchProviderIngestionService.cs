using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IUnifiedSearchProviderIngestionService
{
    Task<UnifiedSearchProviderIngestionResult> IngestAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
