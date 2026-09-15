using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbNowInTheatersMovieCatalog(TmdbApiClient apiClient) : INowInTheatersMovieCatalog
{
    public async Task<MovieProviderSearchResult> GetNowPlayingMoviesAsync(
        string releaseRegion,
        int page,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbNowPlayingQueryBuilder.BuildQuery(releaseRegion, page);
        var response = await apiClient.GetAsync<TmdbMovieSearchResponseJson>(
            $"movie/now_playing?{query}",
            cancellationToken);

        if (response is null)
        {
            return new MovieProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbMovieMapper.ToSummary)
            .ToList();

        return new MovieProviderSearchResult(
            results,
            response.Page == 0 ? page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }
}
