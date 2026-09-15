using MovieApp.Application.Models.Collections;

namespace MovieApp.Application.Services.Collections;

public interface IGetCollectionService
{
    Task<CollectionDetailResult> GetAsync(int tmdbCollectionId, CancellationToken cancellationToken = default);
}
