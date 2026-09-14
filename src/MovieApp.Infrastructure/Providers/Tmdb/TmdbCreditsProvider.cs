using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Credits;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbCreditsProvider(TmdbApiClient apiClient) : ICreditsProvider
{
    public async Task<CreditsResult> GetMovieCreditsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbCreditsResponseJson>(
                $"movie/{tmdbId}/credits",
                cancellationToken);

            return response is null ? new CreditsResult([]) : TmdbCreditsMapper.ToCreditsResult(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new CreditsResult([]);
        }
    }

    public async Task<CreditsResult> GetTvShowCreditsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbAggregateCreditsResponseJson>(
                $"tv/{tmdbId}/aggregate_credits",
                cancellationToken);

            return response is null ? new CreditsResult([]) : TmdbCreditsMapper.ToCreditsResult(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new CreditsResult([]);
        }
    }
}
