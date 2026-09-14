using MovieApp.Domain.Entities;
using MovieApp.Domain.Notifications;

namespace MovieApp.Application.Services.HotRelease;

internal static class HotReleaseNextCheckCalculator
{
    public static DateTime? ComputeNextHotCheckAtUtc(
        IReadOnlyList<Season> seasons,
        DateOnly boundaryDate)
    {
        DateOnly? nextReleaseDate = null;

        foreach (var season in seasons.Where(season => season.SeasonNumber >= 1))
        {
            if (season.AirDate.HasValue && season.AirDate.Value > boundaryDate)
            {
                nextReleaseDate = Min(nextReleaseDate, season.AirDate.Value);
            }

            foreach (var episode in season.Episodes.Where(episode => episode.EpisodeNumber >= 1))
            {
                if (episode.AirDate.HasValue && episode.AirDate.Value > boundaryDate)
                {
                    nextReleaseDate = Min(nextReleaseDate, episode.AirDate.Value);
                }
            }
        }

        return nextReleaseDate.HasValue
            ? ReleaseDateTime.ToReleaseAtUtc(nextReleaseDate.Value)
            : null;
    }

    private static DateOnly Min(DateOnly? current, DateOnly candidate) =>
        current is null || candidate < current.Value
            ? candidate
            : current.Value;
}
