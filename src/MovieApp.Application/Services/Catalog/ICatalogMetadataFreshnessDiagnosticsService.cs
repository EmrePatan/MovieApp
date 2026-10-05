using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.Catalog;

public interface ICatalogMetadataFreshnessDiagnosticsService
{
    Task<CatalogMetadataFreshnessDistribution> GetDiscoveryFreshnessDistributionAsync(
        CancellationToken cancellationToken = default);
}
