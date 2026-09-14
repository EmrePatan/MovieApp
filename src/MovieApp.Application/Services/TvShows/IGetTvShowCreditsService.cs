using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowCreditsService
{
    Task<CreditsResult> GetCreditsAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
