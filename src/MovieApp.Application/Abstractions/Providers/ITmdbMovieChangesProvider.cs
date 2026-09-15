using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Abstractions.Providers;

public interface ITmdbMovieChangesProvider
{
    Task<TmdbChangesPageResult> GetMovieChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default);
}
