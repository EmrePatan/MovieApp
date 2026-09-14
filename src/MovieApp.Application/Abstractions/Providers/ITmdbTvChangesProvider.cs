using MovieApp.Application.Models.TvShowChanges;

namespace MovieApp.Application.Abstractions.Providers;

public interface ITmdbTvChangesProvider
{
    Task<TmdbTvChangesPageResult> GetTvChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default);
}
