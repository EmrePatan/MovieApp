using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IReleaseDetectionCatalogRepository
{
    Task<IReadOnlyList<Season>> GetSeasonsWithEpisodesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<int>> GetSeasonNumbersNeedingEpisodeHydrationAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default)
    {
        var seasons = await GetSeasonsWithEpisodesAsync(tvShowId, cancellationToken);
        return seasons
            .Where(season =>
                season.SeasonNumber >= 1 &&
                season.EpisodeCount is > 0 &&
                (season.AirDate.HasValue && season.AirDate.Value <= boundaryDate ||
                 season.Episodes.Any(episode =>
                     episode.EpisodeNumber >= 1 &&
                     episode.AirDate.HasValue &&
                     episode.AirDate.Value <= boundaryDate)) &&
                season.Episodes.Count(episode => episode.EpisodeNumber >= 1) < season.EpisodeCount.Value)
            .Select(season => season.SeasonNumber)
            .ToList();
    }
}
