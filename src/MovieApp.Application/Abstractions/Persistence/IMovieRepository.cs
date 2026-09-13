using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IMovieRepository
{
    Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<Movie> UpsertFromProviderAsync(
        MovieProviderDetails details,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<MovieProviderSummary> summaries,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
