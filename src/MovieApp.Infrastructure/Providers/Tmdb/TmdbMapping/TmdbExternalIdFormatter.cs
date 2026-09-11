namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbExternalIdFormatter
{
    private const string Prefix = "tmdb-";

    internal static string ToExternalId(int tmdbId) => $"{Prefix}{tmdbId}";

    internal static bool TryParseExternalId(string externalId, out int tmdbId)
    {
        tmdbId = 0;

        if (string.IsNullOrWhiteSpace(externalId))
        {
            return false;
        }

        var value = externalId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? externalId[Prefix.Length..]
            : externalId;

        return int.TryParse(value, out tmdbId) && tmdbId > 0;
    }
}
