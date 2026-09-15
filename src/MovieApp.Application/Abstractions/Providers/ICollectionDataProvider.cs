using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface ICollectionDataProvider
{
    Task<CollectionProviderDetails?> GetCollectionAsync(
        int tmdbCollectionId,
        CancellationToken cancellationToken = default);
}
