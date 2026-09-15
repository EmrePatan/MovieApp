using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.RegionalRelease;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbMovieReleaseDatesProvider(TmdbApiClient apiClient) : IMovieReleaseDatesProvider
{
    public async Task<IReadOnlyList<RegionalMovieReleaseEntry>> GetMovieReleaseDatesAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbMovieReleaseDatesResponseJson>(
                $"movie/{tmdbId}/release_dates",
                cancellationToken);

            return TmdbMovieReleaseDatesMapper.ToRegionalMovieReleaseEntries(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return [];
        }
    }
}
