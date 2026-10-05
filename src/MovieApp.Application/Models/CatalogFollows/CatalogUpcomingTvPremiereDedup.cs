namespace MovieApp.Application.Models.CatalogFollows;

/// <summary>
/// Suppresses TV series premiere rows when the same show already has an eligible S01E01
/// episode item on the identical release date in the merged Coming Up candidate set.
/// </summary>
public static class CatalogUpcomingTvPremiereDedup
{
    public static List<CatalogUpcomingItemResult> Apply(IReadOnlyList<CatalogUpcomingItemResult> items) =>
        SuppressShadowedTvShowPremieres(
            items,
            static item => item.UpcomingKind,
            static item => item.ContentId,
            static item => item.ReleaseDate,
            static item => item.SeasonNumber,
            static item => item.EpisodeNumber);

    public static List<T> SuppressShadowedTvShowPremieres<T>(
        IReadOnlyList<T> items,
        Func<T, CatalogUpcomingKind> upcomingKind,
        Func<T, Guid> contentId,
        Func<T, DateOnly> releaseDate,
        Func<T, int?> seasonNumber,
        Func<T, int?> episodeNumber)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var shadowKeys = new HashSet<(Guid ContentId, DateOnly ReleaseDate)>();
        foreach (var item in items)
        {
            if (IsEligibleSeasonOneEpisodeOne(upcomingKind(item), seasonNumber(item), episodeNumber(item)))
            {
                shadowKeys.Add((contentId(item), releaseDate(item)));
            }
        }

        if (shadowKeys.Count == 0)
        {
            return items.ToList();
        }

        return items
            .Where(item => !ShouldSuppressPremiere(
                upcomingKind(item),
                contentId(item),
                releaseDate(item),
                shadowKeys))
            .ToList();
    }

    private static bool IsEligibleSeasonOneEpisodeOne(
        CatalogUpcomingKind kind,
        int? seasonNumber,
        int? episodeNumber) =>
        kind == CatalogUpcomingKind.TvEpisode
        && seasonNumber == 1
        && episodeNumber == 1;

    private static bool ShouldSuppressPremiere(
        CatalogUpcomingKind kind,
        Guid contentId,
        DateOnly releaseDate,
        HashSet<(Guid ContentId, DateOnly ReleaseDate)> shadowKeys) =>
        kind == CatalogUpcomingKind.TvShowPremiere
        && shadowKeys.Contains((contentId, releaseDate));
}
