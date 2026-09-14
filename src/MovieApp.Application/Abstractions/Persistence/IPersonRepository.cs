using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IPersonRepository
{
    Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<Person> UpsertFromProviderAsync(
        int tmdbId,
        string name,
        string? profilePath,
        CancellationToken cancellationToken = default);
}
