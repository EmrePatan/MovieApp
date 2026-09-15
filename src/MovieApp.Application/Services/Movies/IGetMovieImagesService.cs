using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieImagesService
{
    Task<ImagesResult> GetImagesAsync(
        Guid movieId,
        string? language,
        CancellationToken cancellationToken = default);
}
