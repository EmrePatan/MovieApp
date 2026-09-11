using MovieApp.Application.Abstractions.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeTvExternalIdResolver : ITvShowExternalIdResolver
{
    public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId)
    {
        if (!tmdbId.HasValue)
        {
            return null;
        }

        return $"fake-tv-{tmdbId.Value}";
    }
}
