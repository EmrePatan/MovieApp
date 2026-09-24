using MovieApp.Application.Abstractions.Providers;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.MdbList;

public sealed class NullExternalRatingsProvider : IExternalRatingsProvider
{
    public Task<ExternalRatingsProviderFetchResult?> FetchAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ExternalRatingsProviderFetchResult?>(null);
}
