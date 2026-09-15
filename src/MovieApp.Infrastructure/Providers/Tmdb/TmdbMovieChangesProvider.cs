using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbMovieChangesProvider(TmdbApiClient apiClient) : ITmdbMovieChangesProvider
{
    public async Task<TmdbChangesPageResult> GetMovieChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<TmdbTvChangesResponseJson>(
            $"movie/changes?start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}&page={page}",
            cancellationToken);

        if (response is null)
        {
            return new TmdbChangesPageResult([], page, 0);
        }

        var changedTmdbIds = response.Results
            .Select(item => item.Id)
            .ToList();

        return new TmdbChangesPageResult(
            changedTmdbIds,
            response.Page == 0 ? page : response.Page,
            response.TotalPages);
    }
}
