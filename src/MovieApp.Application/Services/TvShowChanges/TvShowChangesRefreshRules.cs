using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShowChanges;

internal static class TvShowChangesRefreshRules
{
    public static IReadOnlyList<int> DetermineSeasonsToHydrate(
        IReadOnlyList<Season> seasons,
        DateOnly boundaryDate)
    {
        var seasonsToHydrate = new List<int>();

        foreach (var season in seasons.Where(season => season.SeasonNumber >= 1))
        {
            var regularEpisodes = season.Episodes
                .Where(episode => episode.EpisodeNumber >= 1)
                .ToList();

            if (TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(
                    season,
                    regularEpisodes.Count,
                    boundaryDate,
                    regularEpisodes))
            {
                seasonsToHydrate.Add(season.SeasonNumber);
            }
        }

        return seasonsToHydrate;
    }
}
