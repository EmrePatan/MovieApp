using MovieApp.Application.Models.Images;

namespace MovieApp.Application.Abstractions.Providers;

public interface IImageProvider
{
    Task<ProviderImagesResult?> GetMovieImagesAsync(
        int tmdbId,
        string? language,
        CancellationToken cancellationToken = default);

    Task<ProviderImagesResult?> GetTvShowImagesAsync(
        int tmdbId,
        string? language,
        CancellationToken cancellationToken = default);

    Task<ProviderImagesResult?> GetPersonImagesAsync(
        int tmdbPersonId,
        CancellationToken cancellationToken = default);
}
