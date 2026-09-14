using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowFollows;

internal static class TvShowFollowBaselineCatalogRules
{
    public static bool NeedsSeasonSummaries(IEnumerable<Season> seasons) =>
        !seasons.Any(season => season.SeasonNumber >= 1);

    public static bool IsReleaseRelevant(
        Season season,
        DateOnly boundaryDate,
        IReadOnlyList<Episode> regularEpisodes)
    {
        if (season.SeasonNumber < 1)
        {
            return false;
        }

        if (season.AirDate.HasValue && season.AirDate.Value <= boundaryDate)
        {
            return true;
        }

        return regularEpisodes.Any(episode =>
            episode.AirDate.HasValue &&
            episode.AirDate.Value <= boundaryDate);
    }

    public static bool NeedsEpisodeHydration(
        Season season,
        int persistedRegularEpisodeCount,
        DateOnly boundaryDate,
        IReadOnlyList<Episode> regularEpisodes)
    {
        if (season.SeasonNumber < 1)
        {
            return false;
        }

        if (season.EpisodeCount is not > 0)
        {
            return false;
        }

        if (!IsReleaseRelevant(season, boundaryDate, regularEpisodes))
        {
            return false;
        }

        return persistedRegularEpisodeCount < season.EpisodeCount.Value;
    }
}
