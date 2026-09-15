using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Services.People;

public interface IGetPersonImagesService
{
    Task<ImagesResult> GetImagesAsync(int tmdbPersonId, CancellationToken cancellationToken = default);
}
