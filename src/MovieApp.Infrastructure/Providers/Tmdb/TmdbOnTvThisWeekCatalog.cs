using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbOnTvThisWeekCatalog(TmdbApiClient apiClient) : IOnTvThisWeekCatalog
{
    public async Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
        int page,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbOnTheAirQueryBuilder.BuildQuery(page);
        var response = await apiClient.GetAsync<TmdbTvSearchResponseJson>(
            $"tv/on_the_air?{query}",
            cancellationToken);

        if (response is null)
        {
            return new TvShowProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbTvShowMapper.ToSummary)
            .ToList();

        return new TvShowProviderSearchResult(
            results,
            response.Page == 0 ? page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }
}
