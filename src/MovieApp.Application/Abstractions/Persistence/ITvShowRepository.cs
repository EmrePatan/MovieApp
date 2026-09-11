using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvShowRepository
{
    Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<TvShow> UpsertFromProviderAsync(
        TvShowProviderDetails details,
        CancellationToken cancellationToken = default);
}
