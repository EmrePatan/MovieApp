using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Videos;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbVideoProvider(TmdbApiClient apiClient) : IVideoProvider
{
    public async Task<IReadOnlyList<ProviderVideoResult>> GetMovieVideosAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbVideosResponseJson>(
                $"movie/{tmdbId}/videos",
                cancellationToken);

            return response is null ? [] : TmdbVideosMapper.ToProviderVideoResults(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<ProviderVideoResult>> GetTvShowVideosAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbVideosResponseJson>(
                $"tv/{tmdbId}/videos",
                cancellationToken);

            return response is null ? [] : TmdbVideosMapper.ToProviderVideoResults(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }
    }
}
