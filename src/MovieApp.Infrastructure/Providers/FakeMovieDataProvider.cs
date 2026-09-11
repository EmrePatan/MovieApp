using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeMovieDataProvider(MovieDataProviderCallTracker callTracker) : IMovieDataProvider
{
    public const string InterstellarExternalId = "fake-tmdb-900001";
    public const int InterstellarTmdbId = 900001;
    public const int InterstellarTvdbId = 900002;
    public const string InterstellarImdbId = "tt9000001";
    public const string PagedCatalogQueryToken = "paged-catalog";
    public const int PagedCatalogMovieCount = 25;

    private static readonly MovieProviderDetails InterstellarDetails = new(
        ExternalId: InterstellarExternalId,
        TmdbId: InterstellarTmdbId,
        TvdbId: InterstellarTvdbId,
        ImdbId: InterstellarImdbId,
        Title: "Interstellar",
        OriginalTitle: "Interstellar",
        Overview: "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival.",
        ReleaseDate: new DateOnly(2014, 11, 7),
        RuntimeMinutes: 169,
        PosterPath: "/fake/interstellar-poster.jpg",
        BackdropPath: "/fake/interstellar-backdrop.jpg",
        OriginalLanguage: "en",
        VoteAverage: 8.7m,
        VoteCount: 25000,
        Genres: ["Adventure", "Drama", "Science Fiction"]);

    private static readonly IReadOnlyList<MovieProviderSummary> PagedCatalogSummaries =
        Enumerable.Range(1, PagedCatalogMovieCount)
            .Select(index => new MovieProviderSummary(
                ExternalId: $"fake-tmdb-{910000 + index}",
                TmdbId: 910000 + index,
                TvdbId: null,
                ImdbId: $"tt910000{index}",
                Title: $"Fake Movie {index}",
                Overview: $"Overview for fake movie {index}.",
                ReleaseDate: new DateOnly(2020, 1, 1).AddDays(index),
                PosterPath: $"/fake/movie-{index}-poster.jpg",
                VoteAverage: 7.0m,
                VoteCount: 100 + index))
            .ToList();

    public Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        callTracker.RecordSearchMovies();

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = QueryNormalizer.Normalize(query);

        if (normalizedQuery.Contains(PagedCatalogQueryToken, StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePagedResult(PagedCatalogSummaries, page, pageSize));
        }

        if (!normalizedQuery.Contains("interstellar", StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePagedResult([], page, pageSize));
        }

        var summary = new MovieProviderSummary(
            InterstellarDetails.ExternalId,
            InterstellarDetails.TmdbId,
            InterstellarDetails.TvdbId,
            InterstellarDetails.ImdbId,
            InterstellarDetails.Title,
            InterstellarDetails.Overview,
            InterstellarDetails.ReleaseDate,
            InterstellarDetails.PosterPath,
            InterstellarDetails.VoteAverage,
            InterstellarDetails.VoteCount);

        return Task.FromResult(CreatePagedResult([summary], page, pageSize));
    }

    public Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(externalId, InterstellarExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<MovieProviderDetails?>(InterstellarDetails);
        }

        if (TryParsePagedCatalogExternalId(externalId, out var index))
        {
            return Task.FromResult<MovieProviderDetails?>(CreatePagedCatalogDetails(index));
        }

        return Task.FromResult<MovieProviderDetails?>(null);
    }

    private static MovieProviderSearchResult CreatePagedResult(
        IReadOnlyList<MovieProviderSummary> allResults,
        int page,
        int pageSize)
    {
        var totalCount = allResults.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (page - 1) * pageSize;
        var pageResults = allResults.Skip(skip).Take(pageSize).ToList();

        return new MovieProviderSearchResult(
            pageResults,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static bool TryParsePagedCatalogExternalId(string externalId, out int index)
    {
        const string prefix = "fake-tmdb-";

        if (!externalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            index = 0;
            return false;
        }

        if (!int.TryParse(externalId[prefix.Length..], out var tmdbId))
        {
            index = 0;
            return false;
        }

        index = tmdbId - 910000;
        return index >= 1 && index <= PagedCatalogMovieCount;
    }

    private static MovieProviderDetails CreatePagedCatalogDetails(int index) =>
        new(
            ExternalId: $"fake-tmdb-{910000 + index}",
            TmdbId: 910000 + index,
            TvdbId: null,
            ImdbId: $"tt910000{index}",
            Title: $"Fake Movie {index}",
            OriginalTitle: $"Fake Movie {index}",
            Overview: $"Overview for fake movie {index}.",
            ReleaseDate: new DateOnly(2020, 1, 1).AddDays(index),
            RuntimeMinutes: 100 + index,
            PosterPath: $"/fake/movie-{index}-poster.jpg",
            BackdropPath: $"/fake/movie-{index}-backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 7.0m,
            VoteCount: 100 + index,
            Genres: ["Drama"]);
}
