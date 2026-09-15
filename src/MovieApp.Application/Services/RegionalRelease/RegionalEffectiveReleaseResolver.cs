using MovieApp.Application.Models.RegionalRelease;
using MovieApp.Application.Validation;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.RegionalRelease;

public sealed class RegionalEffectiveReleaseResolver : IRegionalEffectiveReleaseResolver
{
    public RegionalEffectiveReleaseResult Resolve(
        string region,
        IReadOnlyList<RegionalMovieReleaseEntry> entries,
        DateOnly? globalReleaseDate)
    {
        var normalizedRegion = WatchProviderRegionValidator.Normalize(region);
        var consumerCandidates = entries
            .Where(entry => string.Equals(
                WatchProviderRegionValidator.Normalize(entry.Region),
                normalizedRegion,
                StringComparison.Ordinal))
            .Where(entry => entry.Type is TmdbReleaseType.Theatrical or TmdbReleaseType.Digital)
            .ToList();

        if (consumerCandidates.Count > 0)
        {
            var earliestDate = consumerCandidates.Min(entry => entry.ReleaseDate);
            var winningEntry = consumerCandidates
                .Where(entry => entry.ReleaseDate == earliestDate)
                .OrderBy(entry => entry.Type == TmdbReleaseType.Theatrical ? 0 : 1)
                .ThenBy(entry => string.IsNullOrWhiteSpace(entry.Certification) ? 1 : 0)
                .ThenBy(entry => entry.SourceIndex)
                .First();

            return new RegionalEffectiveReleaseResult(
                winningEntry.ReleaseDate,
                winningEntry.Type,
                NormalizeCertification(winningEntry.Certification),
                IsFallbackGlobal: false);
        }

        if (globalReleaseDate.HasValue)
        {
            return new RegionalEffectiveReleaseResult(
                globalReleaseDate,
                null,
                null,
                IsFallbackGlobal: true);
        }

        return new RegionalEffectiveReleaseResult(
            null,
            null,
            null,
            IsFallbackGlobal: true);
    }

    internal static string? NormalizeCertification(string? certification)
    {
        if (string.IsNullOrWhiteSpace(certification))
        {
            return null;
        }

        return certification.Trim();
    }
}
