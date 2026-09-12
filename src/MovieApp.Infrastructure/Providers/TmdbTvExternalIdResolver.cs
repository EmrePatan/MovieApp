using MovieApp.Application.Abstractions.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

namespace MovieApp.Infrastructure.Providers;

public sealed class TmdbTvExternalIdResolver : ITvShowExternalIdResolver
{
    public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId)
    {
        if (!tmdbId.HasValue)
        {
            return null;
        }

        return TmdbExternalIdFormatter.ToExternalId(tmdbId.Value);
    }
}
