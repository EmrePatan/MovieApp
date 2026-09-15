using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.Collections;

internal static class CollectionPartPolicy
{
    internal static IReadOnlyList<CollectionProviderPart> Apply(IReadOnlyList<CollectionProviderPart> parts)
    {
        var filtered = parts
            .Where(part => !part.Adult)
            .GroupBy(part => part.TmdbId)
            .Select(group => group.First())
            .ToList();

        var dated = filtered
            .Where(part => part.ReleaseDate.HasValue)
            .OrderBy(part => part.ReleaseDate)
            .ThenBy(part => part.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(part => part.TmdbId);

        var undated = filtered
            .Where(part => !part.ReleaseDate.HasValue)
            .OrderBy(part => part.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(part => part.TmdbId);

        return dated.Concat(undated).ToList();
    }
}
