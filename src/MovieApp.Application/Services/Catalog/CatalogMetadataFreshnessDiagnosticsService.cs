using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.Catalog;

public sealed class CatalogMetadataFreshnessDiagnosticsService(
    ICatalogMetadataFreshnessRepository freshnessRepository) : ICatalogMetadataFreshnessDiagnosticsService
{
    public Task<CatalogMetadataFreshnessDistribution> GetDiscoveryFreshnessDistributionAsync(
        CancellationToken cancellationToken = default) =>
        freshnessRepository.GetDiscoveryFreshnessDistributionAsync(DateTime.UtcNow, cancellationToken);
}
