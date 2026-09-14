using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogReleaseEventRepository
{
    Task<HashSet<string>> GetDedupeKeysForTvShowAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<CatalogReleaseEventInsertResult> TryAddEventsAsync(
        IReadOnlyList<CatalogReleaseEvent> events,
        CancellationToken cancellationToken = default);
}
