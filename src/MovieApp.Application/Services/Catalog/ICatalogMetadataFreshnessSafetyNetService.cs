using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.Catalog;

public interface ICatalogMetadataFreshnessSafetyNetService
{
    Task<CatalogMetadataFreshnessSafetyNetResult> RunAsync(CancellationToken cancellationToken = default);
}
