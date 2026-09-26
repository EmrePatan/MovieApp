using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Validation;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeMovieDataProvider(MovieDataProviderCallTracker callTracker) : IMovieDataProvider
{
    public const string InterstellarExternalId = "fake-tmdb-900001";
    public const int InterstellarTmdbId = 900001;
    public const int InterstellarTvdbId = 900002;
    public const string InterstellarImdbId = "tt9000001";
    public const string PagedCatalogQueryToken = "paged-catalog";
    public const string EmptyImdbCatalogQueryToken = "avatar-empty-imdb";
    public const string DuplicateImdbCatalogQueryToken = "duplicate-imdb";
    public const int PagedCatalogMovieCount = 25;
    public const int EmptyImdbMovieOneTmdbId = 348369;
    public const int EmptyImdbMovieTwoTmdbId = 350632;
    public const int DuplicateImdbMovieOneTmdbId = 930001;
    public const int DuplicateImdbMovieTwoTmdbId = 930002;
    public const string DuplicateImdbMovieImdbId = "tt9300001";
    public const string PosterlessExternalId = "fake-tmdb-900050";
    public const int PosterlessTmdbId = 900050;
    public const string PosterlessTitle = "Posterless Title";

    private const string InterstellarTitleToken = "interstellar";

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

    private static readonly MovieProviderDetails PosterlessDetails = new(
        ExternalId: PosterlessExternalId,
        TmdbId: PosterlessTmdbId,
        TvdbId: null,
        ImdbId: "tt9000050",
        Title: PosterlessTitle,
        OriginalTitle: PosterlessTitle,
        Overview: "A catalog item used to verify null poster handling.",
        ReleaseDate: new DateOnly(2015, 5, 1),
        RuntimeMinutes: 100,
        PosterPath: null,
        BackdropPath: null,
        OriginalLanguage: "en",
        VoteAverage: 5.0m,
        VoteCount: 10,
        Genres: ["Drama"]);

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

        if (normalizedQuery.Contains(EmptyImdbCatalogQueryToken, StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePagedResult(CreateEmptyImdbSummaries(), page, pageSize));
        }

        if (normalizedQuery.Contains(DuplicateImdbCatalogQueryToken, StringComparison.Ordinal))
        {
            return Task.FromResult(CreatePagedResult(CreateDuplicateImdbSummaries(), page, pageSize));
        }

        if (MatchesCatalogTitle(normalizedQuery, QueryNormalizer.Normalize(PosterlessTitle)))
        {
            return Task.FromResult(CreatePagedResult([ToSummary(PosterlessDetails)], page, pageSize));
        }

        if (!MatchesCatalogTitle(normalizedQuery, InterstellarTitleToken))
        {
            return Task.FromResult(CreatePagedResult([], page, pageSize));
        }

        return Task.FromResult(CreatePagedResult([ToSummary(InterstellarDetails)], page, pageSize));
    }

    public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        callTracker.RecordDiscoverMovies();
        cancellationToken.ThrowIfCancellationRequested();

        if (callTracker.FailDiscoverMovies)
        {
            throw new InvalidOperationException("Simulated discover provider failure.");
        }

        return Task.FromResult(FakeDiscoverCatalog.DiscoverMovies(criteria, pageSize: 20));
    }

    public Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        callTracker.RecordAdvancedDiscoverMovies();
        cancellationToken.ThrowIfCancellationRequested();

        if (callTracker.FailAdvancedDiscoverMovies)
        {
            throw new InvalidOperationException("Simulated advanced discover provider failure.");
        }

        var mapped = FakeDiscoverCatalog.MapAdvancedToDiscoverCriteria(criteria);
        return Task.FromResult(FakeDiscoverCatalog.DiscoverMovies(mapped, pageSize: 20));
    }

    public Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        bool includeKeywords = false,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(externalId, InterstellarExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<MovieProviderDetails?>(InterstellarDetails);
        }

        if (string.Equals(externalId, PosterlessExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<MovieProviderDetails?>(PosterlessDetails);
        }

        if (TryParsePagedCatalogExternalId(externalId, out var index))
        {
            return Task.FromResult<MovieProviderDetails?>(CreatePagedCatalogDetails(index));
        }

        if (TryParseEmptyImdbExternalId(externalId, out var emptyImdbIndex))
        {
            return Task.FromResult<MovieProviderDetails?>(CreateEmptyImdbDetails(emptyImdbIndex));
        }

        if (TryParseDuplicateImdbExternalId(externalId, out var duplicateImdbIndex))
        {
            return Task.FromResult<MovieProviderDetails?>(CreateDuplicateImdbDetails(duplicateImdbIndex));
        }

        return Task.FromResult<MovieProviderDetails?>(null);
    }

    private static bool MatchesCatalogTitle(string normalizedQuery, string normalizedTitle) =>
        normalizedQuery.Length >= AdvancedSearchValidator.MinimumQueryLength &&
        normalizedTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);

    private static MovieProviderSummary ToSummary(MovieProviderDetails details) =>
        new(
            details.ExternalId,
            details.TmdbId,
            details.TvdbId,
            details.ImdbId,
            details.Title,
            details.Overview,
            details.ReleaseDate,
            details.PosterPath,
            details.VoteAverage,
            details.VoteCount);

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

    private static IReadOnlyList<MovieProviderSummary> CreateEmptyImdbSummaries() =>
    [
        CreateEmptyImdbSummary(1),
        CreateEmptyImdbSummary(2)
    ];

    private static MovieProviderSummary CreateEmptyImdbSummary(int index) =>
        new(
            ExternalId: $"fake-tmdb-{EmptyImdbMovieOneTmdbId + index - 1}",
            TmdbId: EmptyImdbMovieOneTmdbId + index - 1,
            TvdbId: null,
            ImdbId: null,
            Title: index == 1 ? "Avatar Days" : "The Inception of Dramatic Representation",
            Overview: $"Overview for empty IMDb movie {index}.",
            ReleaseDate: new DateOnly(2020, 1, 1).AddDays(index),
            PosterPath: $"/fake/empty-imdb-{index}-poster.jpg",
            VoteAverage: 6.0m,
            VoteCount: 10 + index);

    private static MovieProviderDetails CreateEmptyImdbDetails(int index) =>
        new(
            ExternalId: $"fake-tmdb-{EmptyImdbMovieOneTmdbId + index - 1}",
            TmdbId: EmptyImdbMovieOneTmdbId + index - 1,
            TvdbId: null,
            ImdbId: string.Empty,
            Title: index == 1 ? "Avatar Days" : "The Inception of Dramatic Representation",
            OriginalTitle: index == 1 ? "Avatar Days" : "The Inception of Dramatic Representation",
            Overview: $"Overview for empty IMDb movie {index}.",
            ReleaseDate: new DateOnly(2020, 1, 1).AddDays(index),
            RuntimeMinutes: 90 + index,
            PosterPath: $"/fake/empty-imdb-{index}-poster.jpg",
            BackdropPath: $"/fake/empty-imdb-{index}-backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 6.0m,
            VoteCount: 10 + index,
            Genres: ["Documentary"]);

    private static IReadOnlyList<MovieProviderSummary> CreateDuplicateImdbSummaries() =>
    [
        CreateDuplicateImdbSummary(1),
        CreateDuplicateImdbSummary(2)
    ];

    private static MovieProviderSummary CreateDuplicateImdbSummary(int index) =>
        new(
            ExternalId: $"fake-tmdb-{DuplicateImdbMovieOneTmdbId + index - 1}",
            TmdbId: DuplicateImdbMovieOneTmdbId + index - 1,
            TvdbId: null,
            ImdbId: DuplicateImdbMovieImdbId,
            Title: $"Duplicate IMDb Movie {index}",
            Overview: $"Overview for duplicate IMDb movie {index}.",
            ReleaseDate: new DateOnly(2021, 1, 1).AddDays(index),
            PosterPath: $"/fake/duplicate-imdb-{index}-poster.jpg",
            VoteAverage: 5.0m,
            VoteCount: 20 + index);

    private static MovieProviderDetails CreateDuplicateImdbDetails(int index) =>
        new(
            ExternalId: $"fake-tmdb-{DuplicateImdbMovieOneTmdbId + index - 1}",
            TmdbId: DuplicateImdbMovieOneTmdbId + index - 1,
            TvdbId: null,
            ImdbId: DuplicateImdbMovieImdbId,
            Title: $"Duplicate IMDb Movie {index}",
            OriginalTitle: $"Duplicate IMDb Movie {index}",
            Overview: $"Overview for duplicate IMDb movie {index}.",
            ReleaseDate: new DateOnly(2021, 1, 1).AddDays(index),
            RuntimeMinutes: 95 + index,
            PosterPath: $"/fake/duplicate-imdb-{index}-poster.jpg",
            BackdropPath: $"/fake/duplicate-imdb-{index}-backdrop.jpg",
            OriginalLanguage: "en",
            VoteAverage: 5.0m,
            VoteCount: 20 + index,
            Genres: ["Drama"]);

    private static bool TryParseEmptyImdbExternalId(string externalId, out int index)
    {
        if (!TryParseTmdbExternalId(externalId, out var tmdbId))
        {
            index = 0;
            return false;
        }

        index = tmdbId - EmptyImdbMovieOneTmdbId + 1;
        return index is >= 1 and <= 2;
    }

    private static bool TryParseDuplicateImdbExternalId(string externalId, out int index)
    {
        if (!TryParseTmdbExternalId(externalId, out var tmdbId))
        {
            index = 0;
            return false;
        }

        index = tmdbId - DuplicateImdbMovieOneTmdbId + 1;
        return index is >= 1 and <= 2;
    }

    private static bool TryParseTmdbExternalId(string externalId, out int tmdbId)
    {
        const string prefix = "fake-tmdb-";

        if (!externalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            tmdbId = 0;
            return false;
        }

        return int.TryParse(externalId[prefix.Length..], out tmdbId);
    }
}
