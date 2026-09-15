using MovieApp.Application.Models.Videos;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieVideosService
{
    Task<VideosResult> GetVideosAsync(Guid movieId, CancellationToken cancellationToken = default);
}
