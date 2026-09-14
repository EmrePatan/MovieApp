using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.WatchProviders;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbWatchProviderService(TmdbApiClient apiClient) : IWatchProviderService
{
    public async Task<WatchProvidersResult> GetMovieWatchProvidersAsync(
        int tmdbId,
        string region,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbWatchProvidersResponseJson>(
                $"movie/{tmdbId}/watch/providers",
                cancellationToken);

            return response is null
                ? new WatchProvidersResult(region, [], null)
                : TmdbWatchProvidersMapper.ToWatchProvidersResult(response, region);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new WatchProvidersResult(region, [], null);
        }
    }

    public async Task<WatchProvidersResult> GetTvShowWatchProvidersAsync(
        int tmdbId,
        string region,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbWatchProvidersResponseJson>(
                $"tv/{tmdbId}/watch/providers",
                cancellationToken);

            return response is null
                ? new WatchProvidersResult(region, [], null)
                : TmdbWatchProvidersMapper.ToWatchProvidersResult(response, region);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new WatchProvidersResult(region, [], null);
        }
    }
}
