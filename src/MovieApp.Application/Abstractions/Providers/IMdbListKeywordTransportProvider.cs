using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Providers;

public interface IMdbListKeywordTransportProvider
{
    Task<MdbListKeywordsTransportResult?> FetchKeywordsAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default);

    Task<MdbListKeywordsBatchTransportResult?> FetchKeywordsBatchAsync(
        CatalogContentType mediaType,
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default);
}
