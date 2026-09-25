using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.TvShows;

public sealed class TvShowExternalIdLookup(ITvShowRepository tvShowRepository) : ITvShowExternalIdLookup
{
    public Task<TvShowExternalIds?> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
        tvShowRepository.GetExternalIdsByIdAsync(tvShowId, cancellationToken);
}
