namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogChangesRelevanceRepository
{
    Task<IReadOnlyDictionary<int, Guid>> GetRelevantMovieIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetRelevantTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);
}
