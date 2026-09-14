using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IReleaseDetectionCatalogRepository
{
    Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
