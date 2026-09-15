using MovieApp.Application.Models.Providers;
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

    Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<PersonProviderSummary> summaries,
        CancellationToken cancellationToken = default);
}
