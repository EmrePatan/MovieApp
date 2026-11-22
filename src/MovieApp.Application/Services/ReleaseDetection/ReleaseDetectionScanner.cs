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
            var simultaneousFullSeasonRelease = IsSimultaneousFullSeasonRelease(season);
            var premiereDate = ResolveSeasonPremiereDate(season);
            var premiereDedupeKey = CatalogReleaseEventDedupeKey.ForSeasonPremiere(tvShowId, season.SeasonNumber);
            var premiereExistsOrWillExist =
                existingDedupeKeys.Contains(premiereDedupeKey) ||
                (premiereDate.HasValue && premiereDate.Value <= boundary);

            foreach (var episode in season.Episodes.Where(episode => episode.EpisodeNumber >= 1))
            {
                if (simultaneousFullSeasonRelease)
                {
                    continue;
                }

                if (!episode.AirDate.HasValue || episode.AirDate.Value > boundary)
                {
                    continue;
                }

                // Multi-episode seasons: same-day as season premiere → only "yayında" / season
                // notify (binge drops + premiere day). Later weekly episodes keep NewEpisode.
                if (ShouldSuppressEpisodeEventOnPremiereDay(
                        season,
                        episode.AirDate.Value,
                        premiereDate,
                        premiereExistsOrWillExist))
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

            if (!premiereDate.HasValue || premiereDate.Value > boundary)
            {
                continue;
            }

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

    private static bool IsSimultaneousFullSeasonRelease(Season season)
    {
        if (season.EpisodeCount is not > 0)
        {
            return false;
        }

        var regularEpisodes = season.Episodes
            .Where(episode => episode.EpisodeNumber >= 1)
            .ToList();

        if (regularEpisodes.Count < season.EpisodeCount ||
            regularEpisodes.Any(episode => !episode.AirDate.HasValue))
        {
            return false;
        }

        return regularEpisodes
            .Select(episode => episode.AirDate!.Value)
            .Distinct()
            .Take(2)
            .Count() == 1;
    }

    private static bool ShouldSuppressEpisodeEventOnPremiereDay(
        Season season,
        DateOnly episodeAirDate,
        DateOnly? premiereDate,
        bool premiereExistsOrWillExist)
    {
        if (!premiereExistsOrWillExist || !premiereDate.HasValue)
        {
            return false;
        }

        if (season.EpisodeCount is not > 1)
        {
            return false;
        }

        return episodeAirDate == premiereDate.Value;
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
