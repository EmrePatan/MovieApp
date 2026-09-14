using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.TvShowChanges;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbTvChangesProvider(TmdbApiClient apiClient) : ITmdbTvChangesProvider
{
    public async Task<TmdbTvChangesPageResult> GetTvChangesPageAsync(
        DateOnly startDate,
        DateOnly endDate,
        int page,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.GetAsync<TmdbTvChangesResponseJson>(
            $"tv/changes?start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}&page={page}",
            cancellationToken);

        if (response is null)
        {
            return new TmdbTvChangesPageResult([], page, 0);
        }

        var changedTmdbIds = response.Results
            .Select(item => item.Id)
            .ToList();

        return new TmdbTvChangesPageResult(
            changedTmdbIds,
            response.Page == 0 ? page : response.Page,
            response.TotalPages);
    }
}
