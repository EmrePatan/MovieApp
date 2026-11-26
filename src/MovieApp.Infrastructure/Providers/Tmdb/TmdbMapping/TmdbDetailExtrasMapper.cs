using MovieApp.Application.Models.Catalog;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbDetailExtrasMapper
{
    private const int TheatricalReleaseType = 3;
    private const int MaxNames = 4;

    internal static CatalogDetailExtras ForMovie(TmdbMovieDetailsResponseJson details) =>
        new()
        {
            Tagline = Text(details.Tagline),
            Certifications = ByCountry(
                (details.ReleaseDates?.Results ?? []).Select(region => (
                    region.Iso31661,
                    // The theatrical rating wins; other release types only fill the gap.
                    region.ReleaseDates
                        .Where(entry => !string.IsNullOrWhiteSpace(entry.Certification))
                        .OrderBy(entry => entry.Type == TheatricalReleaseType ? 0 : 1)
                        .Select(entry => (string?)entry.Certification)
                        .FirstOrDefault()))),
            ProductionCompanies = Names(details.ProductionCompanies),
            Budget = details.Budget > 0 ? details.Budget : null,
            Revenue = details.Revenue > 0 ? details.Revenue : null
        };

    internal static CatalogDetailExtras ForTvShow(TmdbTvDetailsResponseJson details) =>
        new()
        {
            Tagline = Text(details.Tagline),
            Certifications = ByCountry(
                (details.ContentRatings?.Results ?? []).Select(entry => (entry.Iso31661, entry.Rating))),
            Networks = Names(details.Networks),
            Creators = Names(details.CreatedBy),
            ProductionCompanies = Names(details.ProductionCompanies),
            LastEpisode = details.LastEpisodeToAir is { } episode
                ? new CatalogLastEpisode(
                    episode.SeasonNumber,
                    episode.EpisodeNumber,
                    TmdbMovieMapper.ParseReleaseDate(episode.AirDate))
                : null
        };

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> Names(IEnumerable<TmdbNamedJson> entries) =>
        entries
            .Select(entry => Text(entry.Name))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxNames)
            .ToList();

    private static Dictionary<string, string> ByCountry(IEnumerable<(string? Country, string? Rating)> entries)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (country, rating) in entries)
        {
            if (Text(country) is { } code && Text(rating) is { } value)
            {
                result.TryAdd(code.ToUpperInvariant(), value);
            }
        }

        return result;
    }
}
