using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieCreditsService
{
    Task<CreditsResult> GetCreditsAsync(Guid movieId, CancellationToken cancellationToken = default);
}
