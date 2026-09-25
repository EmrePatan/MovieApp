using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Services.WatchHistory;

namespace MovieApp.UnitTests.WatchHistory;

internal sealed class GetSeasonDelegatingRegularSeasonEpisodeIngestionService(IGetSeasonService getSeasonService)
    : IRegularSeasonEpisodeIngestionService
{
    public async Task<RegularSeasonEpisodeIngestionResult> IngestMissingSeasonsAsync(
        Guid tvShowId,
        IReadOnlyList<int> seasonNumbers,
        CancellationToken cancellationToken = default)
    {
        foreach (var seasonNumber in seasonNumbers)
        {
            await getSeasonService.GetSeasonAsync(tvShowId, seasonNumber, cancellationToken);
        }

        return new RegularSeasonEpisodeIngestionResult(
            SeasonsPersisted: seasonNumbers.Count,
            ProviderSeasonFetchWallMs: 0,
            ProviderSeasonFetchAccumulatedMs: 0,
            MaxSeasonProviderFetchMs: 0,
            PersistenceMs: 0,
            SaveChangesCount: seasonNumbers.Count);
    }
}
