namespace MovieApp.Application.Abstractions.Providers;

public interface ITvShowExternalIdResolver
{
    string? Resolve(int? tmdbId, int? tvdbId, string? imdbId);
}
