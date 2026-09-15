using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowImagesService
{
    Task<ImagesResult> GetImagesAsync(
        Guid tvShowId,
        string? language,
        CancellationToken cancellationToken = default);
}
