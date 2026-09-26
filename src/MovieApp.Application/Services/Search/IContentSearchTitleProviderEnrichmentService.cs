using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface IContentSearchTitleProviderEnrichmentService
{
    Task<ContentSearchTitleProviderEnrichmentResult> EnrichFromProviderAsync(
        ContentSearchTitleProviderEnrichmentRequest request,
        CancellationToken cancellationToken = default);
}
