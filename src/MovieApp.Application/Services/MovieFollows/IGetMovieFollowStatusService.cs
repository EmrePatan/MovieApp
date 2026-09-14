using MovieApp.Application.Models.MovieFollows;

namespace MovieApp.Application.Services.MovieFollows;

public interface IGetMovieFollowStatusService
{
    Task<MovieFollowStatusResult> GetAsync(Guid movieId, CancellationToken cancellationToken = default);
}
