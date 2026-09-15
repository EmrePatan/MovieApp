using MovieApp.Application.Models.Videos;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowVideosService
{
    Task<VideosResult> GetVideosAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
