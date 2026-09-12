using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IUnifiedSearchProviderIngestionService
{
    Task IngestAsync(SearchCriteria criteria, CancellationToken cancellationToken = default);
}
