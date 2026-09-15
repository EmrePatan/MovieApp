using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeNowInTheatersMovieCatalog : INowInTheatersMovieCatalog
{
    private static readonly Dictionary<string, IReadOnlyList<MovieProviderSummary>> RegionCatalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["TR"] =
            [
                CreateSummary(920101, "Cinema One"),
                CreateSummary(920102, "Cinema Two"),
                CreateSummary(920103, "Cinema Three"),
            ],
            ["US"] =
            [
                CreateSummary(920201, "Stateside Premiere"),
                CreateSummary(920202, "Stateside Feature"),
            ],
        };

    public Task<MovieProviderSearchResult> GetNowPlayingMoviesAsync(
        string releaseRegion,
        int page,
        CancellationToken cancellationToken = default)
    {
        var normalizedRegion = releaseRegion.Trim().ToUpperInvariant();
        var catalog = RegionCatalog.TryGetValue(normalizedRegion, out var summaries)
            ? summaries
            : [];

        var pageSize = TmdbSearchDefaults.ResultsPerPage;
        var skip = Math.Max(0, page - 1) * pageSize;
        var pageItems = catalog.Skip(skip).Take(pageSize).ToList();
        var totalCount = catalog.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Task.FromResult(new MovieProviderSearchResult(
            pageItems,
            page,
            pageSize,
            totalCount,
            totalPages));
    }

    private static MovieProviderSummary CreateSummary(int tmdbId, string title) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: $"tt{tmdbId}",
            Title: title,
            Overview: $"{title} is currently playing in theaters.",
            ReleaseDate: new DateOnly(2026, 3, 1),
            PosterPath: $"/fake/{tmdbId}-poster.jpg",
            VoteAverage: 7.5m,
            VoteCount: 120);
}
