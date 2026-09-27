using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbOriginCountryMapper
{
    internal static string? ResolvePrimaryOriginCountryCode(IReadOnlyList<TmdbProductionCountryJson>? productionCountries)
    {
        if (productionCountries is null || productionCountries.Count == 0)
        {
            return null;
        }

        foreach (var country in productionCountries)
        {
            if (string.Equals(country.Iso31661, "TR", StringComparison.OrdinalIgnoreCase))
            {
                return "TR";
            }
        }

        return productionCountries[0].Iso31661?.Trim().ToUpperInvariant();
    }

    internal static string? ResolvePrimaryOriginCountryCode(IReadOnlyList<string>? originCountries)
    {
        if (originCountries is null || originCountries.Count == 0)
        {
            return null;
        }

        foreach (var country in originCountries)
        {
            if (string.Equals(country, "TR", StringComparison.OrdinalIgnoreCase))
            {
                return "TR";
            }
        }

        return originCountries[0].Trim().ToUpperInvariant();
    }
}
