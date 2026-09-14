using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.Application.Services.ReleaseDetection;

internal static class ReleaseDetectionScanner
{
    public static IReadOnlyList<CatalogReleaseEvent> DetectMissingEvents(
        Guid tvShowId,
        IReadOnlyList<Season> seasons,
        DateOnly boundary,
        IReadOnlySet<string> existingDedupeKeys,
        CatalogReleaseEventSource source,
        DateTime detectedAtUtc)
    {
        var events = new List<CatalogReleaseEvent>();

        foreach (var season in seasons.Where(season => season.SeasonNumber >= 1))
        {
            foreach (var episode in season.Episodes.Where(episode => episode.EpisodeNumber >= 1))
            {
                if (!episode.AirDate.HasValue || episode.AirDate.Value > boundary)
                {
                    continue;
                }

                var dedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(
                    tvShowId,
                    season.SeasonNumber,
                    episode.EpisodeNumber);

                if (existingDedupeKeys.Contains(dedupeKey))
                {
                    continue;
                }

                events.Add(CatalogReleaseEventFactory.CreateEpisodeEvent(
                    tvShowId,
                    season.SeasonNumber,
                    episode.EpisodeNumber,
                    episode.AirDate.Value,
                    source,
                    detectedAtUtc));
            }

            var premiereDate = ResolveSeasonPremiereDate(season);
            if (!premiereDate.HasValue || premiereDate.Value > boundary)
            {
                continue;
            }

            var premiereDedupeKey = CatalogReleaseEventDedupeKey.ForSeasonPremiere(tvShowId, season.SeasonNumber);
            if (existingDedupeKeys.Contains(premiereDedupeKey))
            {
                continue;
            }

            events.Add(CatalogReleaseEventFactory.CreateSeasonPremiereEvent(
                tvShowId,
                season.SeasonNumber,
                premiereDate.Value,
                source,
                detectedAtUtc));
        }

        return events;
    }

    internal static DateOnly? ResolveSeasonPremiereDate(Season season)
    {
        if (season.SeasonNumber < 1)
        {
            return null;
        }

        var episodeAirDates = season.Episodes
            .Where(episode => episode.EpisodeNumber >= 1 && episode.AirDate.HasValue)
            .Select(episode => episode.AirDate!.Value)
            .ToList();

        if (episodeAirDates.Count > 0)
        {
            return episodeAirDates.Min();
        }

        if (season.EpisodeCount > 0 && season.AirDate.HasValue)
        {
            return season.AirDate;
        }

        return null;
    }
}
