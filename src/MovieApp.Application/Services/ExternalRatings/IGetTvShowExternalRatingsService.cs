using MovieApp.Application.Models.ExternalRatings;

namespace MovieApp.Application.Services.ExternalRatings;

public interface IGetTvShowExternalRatingsService
{
    Task<ExternalRatingsResult> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
