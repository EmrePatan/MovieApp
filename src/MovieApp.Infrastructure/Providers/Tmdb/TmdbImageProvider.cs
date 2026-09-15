using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Images;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbImageProvider(TmdbApiClient apiClient) : IImageProvider
{
    public async Task<ProviderImagesResult?> GetMovieImagesAsync(
        int tmdbId,
        string? language,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbImagesResponseJson>(
                BuildImagesPath($"movie/{tmdbId}/images", language),
                cancellationToken);

            return response is null ? null : TmdbImagesMapper.ToProviderImagesResult(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ProviderImagesResult?> GetTvShowImagesAsync(
        int tmdbId,
        string? language,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbImagesResponseJson>(
                BuildImagesPath($"tv/{tmdbId}/images", language),
                cancellationToken);

            return response is null ? null : TmdbImagesMapper.ToProviderImagesResult(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ProviderImagesResult?> GetPersonImagesAsync(
        int tmdbPersonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbImagesResponseJson>(
                $"person/{tmdbPersonId}/images",
                cancellationToken);

            return response is null ? null : TmdbImagesMapper.ToProviderImagesResult(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string BuildImagesPath(string relativePath, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return relativePath;
        }

        return $"{relativePath}?include_image_language={Uri.EscapeDataString(language)}";
    }
}
