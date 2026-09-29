using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public interface ITransactionalStreamOfferFilter
{
    Task<IReadOnlySet<int>> SelectMatchingTmdbIdsAsync(
        SearchContentType mediaType,
        string? watchRegion,
        IReadOnlyList<int> watchProviderIds,
        IReadOnlyList<WatchMonetizationType> monetizationTypes,
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default);
}
