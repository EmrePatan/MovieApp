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

        return ResolvePrimaryOriginCountryCode(productionCountries.Select(country => country.Iso31661));
    }

    internal static string? ResolvePrimaryOriginCountryCode(IEnumerable<string?>? originCountries)
    {
        if (originCountries is null)
        {
            return null;
        }

        string? firstValid = null;
        foreach (var country in originCountries)
        {
            var normalized = NormalizeCountryCode(country);
            if (normalized is null)
            {
                continue;
            }

            if (firstValid is null)
            {
                firstValid = normalized;
            }

            if (normalized == "TR")
            {
                return "TR";
            }
        }

        return firstValid;
    }

    internal static string? NormalizeCountryCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 2 || !char.IsLetter(trimmed[0]) || !char.IsLetter(trimmed[1]))
        {
            return null;
        }

        return trimmed.ToUpperInvariant();
    }
}
