using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeOnTvThisWeekCatalog : IOnTvThisWeekCatalog
{
    private static readonly IReadOnlyList<TvShowProviderSummary> Catalog =
    [
        CreateSummary(930101, "Airing Drama"),
        CreateSummary(930102, "Weekly Mystery"),
        CreateSummary(930103, "Returning Comedy"),
        CreateSummary(930104, "Late Night Talk"),
    ];

    public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
        int page,
        CancellationToken cancellationToken = default)
    {
        var pageSize = TmdbSearchDefaults.ResultsPerPage;
        var skip = Math.Max(0, page - 1) * pageSize;
        var pageItems = Catalog.Skip(skip).Take(pageSize).ToList();
        var totalCount = Catalog.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Task.FromResult(new TvShowProviderSearchResult(
            pageItems,
            page,
            pageSize,
            totalCount,
            totalPages));
    }

    private static TvShowProviderSummary CreateSummary(int tmdbId, string title) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: $"tt{tmdbId}",
            Title: title,
            OriginalTitle: title,
            Overview: $"{title} has episodes airing this week.",
            FirstAirDate: new DateOnly(2024, 1, 1),
            PosterPath: $"/fake/{tmdbId}-poster.jpg",
            BackdropPath: null,
            OriginalLanguage: "en",
            VoteAverage: 7.2m,
            VoteCount: 80);
}
