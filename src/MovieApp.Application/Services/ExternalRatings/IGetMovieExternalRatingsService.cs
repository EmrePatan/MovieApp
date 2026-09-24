using MovieApp.Application.Models.ExternalRatings;

namespace MovieApp.Application.Services.ExternalRatings;

public interface IGetMovieExternalRatingsService
{
    Task<ExternalRatingsResult> GetAsync(Guid movieId, CancellationToken cancellationToken = default);
}
