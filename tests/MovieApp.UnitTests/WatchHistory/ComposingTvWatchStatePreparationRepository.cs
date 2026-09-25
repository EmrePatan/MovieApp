using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.UnitTests.WatchHistory;

internal sealed class ComposingTvWatchStatePreparationRepository(
    ITvShowRepository tvShowRepository,
    ISeasonRepository seasonRepository,
    IEpisodeRepository episodeRepository) : ITvWatchStatePreparationRepository
{
    public async Task<TvWatchStatePreparation> PrepareAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        if (!await tvShowRepository.ExistsAsync(tvShowId, cancellationToken))
        {
            return TvWatchStatePreparation.NotFound();
        }

        var ingestionCheck = await seasonRepository.CheckRegularEpisodeIngestionRequiredAsync(tvShowId, cancellationToken);
        var episodeIds = await episodeRepository.GetEpisodeIdsForRegularSeasonsAsync(tvShowId, cancellationToken);

        return new TvWatchStatePreparation(
            TvShowExists: true,
            IngestionRequired: ingestionCheck.IsRequired,
            MissingSeasonNumbers: ingestionCheck.MissingSeasonNumbers,
            EpisodeIds: episodeIds,
            RegularSeasonCount: ingestionCheck.RegularSeasonCount,
            SeasonsWithEpisodeRowsCount: ingestionCheck.SeasonsWithEpisodeRowsCount);
    }
}
