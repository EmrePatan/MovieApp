using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShows;

public interface ITvShowSeasonSummaryHydrator
{
    Task<TvShow> EnsureSeasonSummariesAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
