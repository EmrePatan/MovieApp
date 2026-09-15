using MovieApp.Application.Models.RegionalRelease;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbMovieReleaseDatesMapper
{
    public static IReadOnlyList<RegionalMovieReleaseEntry> ToRegionalMovieReleaseEntries(
        TmdbMovieReleaseDatesResponseJson? response)
    {
        if (response?.Results is null || response.Results.Count == 0)
        {
            return [];
        }

        var entries = new List<RegionalMovieReleaseEntry>();
        var sourceIndex = 0;

        foreach (var regionalResult in response.Results)
        {
            if (string.IsNullOrWhiteSpace(regionalResult.Iso31661))
            {
                continue;
            }

            var region = WatchProviderRegionValidator.Normalize(regionalResult.Iso31661);
            if (region.Length != 2)
            {
                continue;
            }

            foreach (var releaseDateEntry in regionalResult.ReleaseDates)
            {
                if (!TryMapReleaseType(releaseDateEntry.Type, out var releaseType))
                {
                    continue;
                }

                if (!TryParseReleaseDate(releaseDateEntry.ReleaseDate, out var releaseDate))
                {
                    continue;
                }

                entries.Add(new RegionalMovieReleaseEntry(
                    region,
                    releaseDate,
                    releaseType,
                    releaseDateEntry.Certification,
                    sourceIndex++));
            }
        }

        return entries;
    }

    private static bool TryMapReleaseType(int type, out TmdbReleaseType releaseType)
    {
        if (!Enum.IsDefined(typeof(TmdbReleaseType), type))
        {
            releaseType = default;
            return false;
        }

        releaseType = (TmdbReleaseType)type;
        return true;
    }

    private static bool TryParseReleaseDate(string? value, out DateOnly releaseDate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            releaseDate = default;
            return false;
        }

        if (DateOnly.TryParse(value, out releaseDate))
        {
            return true;
        }

        if (DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsedDateTime))
        {
            releaseDate = DateOnly.FromDateTime(parsedDateTime);
            return true;
        }

        releaseDate = default;
        return false;
    }
}
