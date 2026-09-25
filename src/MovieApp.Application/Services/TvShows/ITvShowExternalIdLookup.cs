using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.TvShows;

public interface ITvShowExternalIdLookup
{
    Task<TvShowExternalIds?> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
