using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Infrastructure.Providers;

internal static class FakeDiscoverCatalog
{
    public const int DiscoverMovieAlphaTmdbId = 940001;
    public const int DiscoverMovieBetaTmdbId = 940002;
    public const int DiscoverMovieFutureTmdbId = 940003;
    public const int DiscoverTvAlphaTmdbId = 940101;
    public const int DiscoverTvBetaTmdbId = 940102;

    private static readonly IReadOnlyList<MovieProviderSummary> MovieSummaries =
    [
        CreateMovieSummary(
            DiscoverMovieAlphaTmdbId,
            "Discover Movie Alpha",
            new DateOnly(2024, 6, 15),
            voteAverage: 8.4m,
            voteCount: 5000,
            language: "en",
            popularity: 40),
        CreateMovieSummary(
            DiscoverMovieBetaTmdbId,
            "Discover Movie Beta",
            new DateOnly(2023, 3, 10),
            voteAverage: 7.1m,
            voteCount: 900,
            language: "en",
            popularity: 200),
        CreateMovieSummary(
            DiscoverMovieFutureTmdbId,
            "Discover Movie Future",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            voteAverage: 6.0m,
            voteCount: 10,
            language: "en",
            popularity: 5)
    ];

    private static readonly IReadOnlyList<TvShowProviderSummary> TvSummaries =
    [
        CreateTvSummary(
            DiscoverTvAlphaTmdbId,
            "Discover TV Alpha",
            new DateOnly(2024, 5, 1),
            voteAverage: 8.8m,
            voteCount: 4200,
            language: "en",
            popularity: 30),
        CreateTvSummary(
            DiscoverTvBetaTmdbId,
            "Discover TV Beta",
            new DateOnly(2022, 11, 20),
            voteAverage: 7.0m,
            voteCount: 700,
            language: "fr",
            popularity: 180)
    ];

    public static MovieProviderSearchResult DiscoverMovies(
        DiscoverProviderCriteria criteria,
        int pageSize)
    {
        var filtered = ApplyMovieFilters(MovieSummaries, criteria).ToList();
        return CreateMovieResult(filtered, criteria, pageSize);
    }

    public static TvShowProviderSearchResult DiscoverTvShows(
        DiscoverProviderCriteria criteria,
        int pageSize)
    {
        var filtered = ApplyTvFilters(TvSummaries, criteria).ToList();
        return CreateTvResult(filtered, criteria, pageSize);
    }

    private static IEnumerable<MovieProviderSummary> ApplyMovieFilters(
        IEnumerable<MovieProviderSummary> summaries,
        DiscoverProviderCriteria criteria)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var summary in summaries)
        {
            if (criteria.Year.HasValue &&
                (summary.ReleaseDate?.Year != criteria.Year.Value))
            {
                continue;
            }

            if (criteria.MinRating.HasValue && summary.VoteAverage < criteria.MinRating.Value)
            {
                continue;
            }

            if (criteria.MinVoteCount is int minVoteCount && summary.VoteCount < minVoteCount)
            {
                continue;
            }

            if (criteria.Mode == DiscoverBrowseMode.NewReleases &&
                (!summary.ReleaseDate.HasValue || summary.ReleaseDate.Value > today))
            {
                continue;
            }

            if (criteria.Mode == DiscoverBrowseMode.TopRated && summary.VoteCount < 50)
            {
                continue;
            }

            yield return summary;
        }
    }

    private static IEnumerable<TvShowProviderSummary> ApplyTvFilters(
        IEnumerable<TvShowProviderSummary> summaries,
        DiscoverProviderCriteria criteria)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var summary in summaries)
        {
            if (criteria.Year.HasValue &&
                (summary.FirstAirDate?.Year != criteria.Year.Value))
            {
                continue;
            }

            if (criteria.MinRating.HasValue && summary.VoteAverage < criteria.MinRating.Value)
            {
                continue;
            }

            if (criteria.MinVoteCount is int minVoteCount && summary.VoteCount < minVoteCount)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(criteria.Language) &&
                !string.Equals(summary.OriginalLanguage, criteria.Language, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (criteria.Mode == DiscoverBrowseMode.NewReleases &&
                (!summary.FirstAirDate.HasValue || summary.FirstAirDate.Value > today))
            {
                continue;
            }

            if (criteria.Mode == DiscoverBrowseMode.TopRated && summary.VoteCount < 50)
            {
                continue;
            }

            yield return summary;
        }
    }

    private static MovieProviderSearchResult CreateMovieResult(
        IReadOnlyList<MovieProviderSummary> allResults,
        DiscoverProviderCriteria criteria,
        int pageSize)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        var sorted = SortMovies(allResults, criteria.Mode, effectiveSort);
        var totalCount = sorted.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (criteria.Page - 1) * pageSize;
        var pageResults = sorted.Skip(skip).Take(pageSize).ToList();

        return new MovieProviderSearchResult(
            pageResults,
            criteria.Page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static TvShowProviderSearchResult CreateTvResult(
        IReadOnlyList<TvShowProviderSummary> allResults,
        DiscoverProviderCriteria criteria,
        int pageSize)
    {
        var effectiveSort = criteria.Sort ?? DiscoverBrowseValidator.GetDefaultSortForMode(criteria.Mode);
        var sorted = SortTvShows(allResults, criteria.Mode, effectiveSort);
        var totalCount = sorted.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var skip = (criteria.Page - 1) * pageSize;
        var pageResults = sorted.Skip(skip).Take(pageSize).ToList();

        return new TvShowProviderSearchResult(
            pageResults,
            criteria.Page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static List<MovieProviderSummary> SortMovies(
        IReadOnlyList<MovieProviderSummary> summaries,
        DiscoverBrowseMode mode,
        DiscoverBrowseSort sort)
    {
        if (mode == DiscoverBrowseMode.Popular &&
            sort is DiscoverBrowseSort.PopularityDesc or DiscoverBrowseSort.PopularityAsc)
        {
            var ordered = sort == DiscoverBrowseSort.PopularityAsc
                ? summaries.OrderBy(summary => summary.Popularity)
                : summaries.OrderByDescending(summary => summary.Popularity);
            return ordered.ThenBy(summary => summary.TmdbId).ToList();
        }

        return sort switch
        {
            DiscoverBrowseSort.RatingDesc => summaries
                .OrderByDescending(summary => summary.VoteAverage)
                .ThenByDescending(summary => summary.VoteCount)
                .ThenBy(summary => summary.TmdbId)
                .ToList(),
            DiscoverBrowseSort.ReleaseDesc => summaries
                .OrderByDescending(summary => summary.ReleaseDate ?? DateOnly.MinValue)
                .ThenBy(summary => summary.TmdbId)
                .ToList(),
            DiscoverBrowseSort.TitleAsc => summaries
                .OrderBy(summary => summary.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(summary => summary.TmdbId)
                .ToList(),
            _ => summaries
                .OrderByDescending(summary => summary.VoteCount)
                .ThenByDescending(summary => summary.VoteAverage)
                .ThenBy(summary => summary.TmdbId)
                .ToList()
        };
    }

    private static List<TvShowProviderSummary> SortTvShows(
        IReadOnlyList<TvShowProviderSummary> summaries,
        DiscoverBrowseMode mode,
        DiscoverBrowseSort sort)
    {
        if (mode == DiscoverBrowseMode.Popular &&
            sort is DiscoverBrowseSort.PopularityDesc or DiscoverBrowseSort.PopularityAsc)
        {
            var ordered = sort == DiscoverBrowseSort.PopularityAsc
                ? summaries.OrderBy(summary => summary.Popularity)
                : summaries.OrderByDescending(summary => summary.Popularity);
            return ordered.ThenBy(summary => summary.TmdbId).ToList();
        }

        return sort switch
        {
            DiscoverBrowseSort.RatingDesc => summaries
                .OrderByDescending(summary => summary.VoteAverage)
                .ThenByDescending(summary => summary.VoteCount)
                .ThenBy(summary => summary.TmdbId)
                .ToList(),
            DiscoverBrowseSort.ReleaseDesc => summaries
                .OrderByDescending(summary => summary.FirstAirDate ?? DateOnly.MinValue)
                .ThenBy(summary => summary.TmdbId)
                .ToList(),
            DiscoverBrowseSort.TitleAsc => summaries
                .OrderBy(summary => summary.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(summary => summary.TmdbId)
                .ToList(),
            _ => summaries
                .OrderByDescending(summary => summary.VoteCount)
                .ThenByDescending(summary => summary.VoteAverage)
                .ThenBy(summary => summary.TmdbId)
                .ToList()
        };
    }

    private static MovieProviderSummary CreateMovieSummary(
        int tmdbId,
        string title,
        DateOnly? releaseDate,
        decimal voteAverage,
        int voteCount,
        string language,
        decimal popularity = 0) =>
        new(
            ExternalId: $"fake-tmdb-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: $"tt{tmdbId}",
            Title: title,
            Overview: $"Overview for {title}.",
            ReleaseDate: releaseDate,
            PosterPath: $"/fake/discover-movie-{tmdbId}.jpg",
            VoteAverage: voteAverage,
            VoteCount: voteCount,
            Popularity: popularity);

    public static DiscoverProviderCriteria MapAdvancedToDiscoverCriteria(
        AdvancedDiscoverProviderCriteria criteria) =>
        new(
            DiscoverBrowseMode.Trending,
            criteria.Page,
            criteria.GenreTmdbIds,
            criteria.Year ?? criteria.YearFrom,
            criteria.YearFrom,
            criteria.YearTo,
            criteria.MinRating,
            criteria.MinVoteCount,
            criteria.MinRuntimeMinutes,
            criteria.MaxRuntimeMinutes,
            criteria.OriginalLanguage,
            criteria.OriginCountry,
            criteria.KeywordTmdbIds,
            criteria.TvStatuses,
            criteria.Sort switch
            {
                AdvancedDiscoverSort.RatingDesc => DiscoverBrowseSort.RatingDesc,
                AdvancedDiscoverSort.Newest => DiscoverBrowseSort.ReleaseDesc,
                AdvancedDiscoverSort.Oldest => DiscoverBrowseSort.ReleaseAsc,
                _ => DiscoverBrowseSort.PopularityDesc
            });

    private static TvShowProviderSummary CreateTvSummary(
        int tmdbId,
        string title,
        DateOnly? firstAirDate,
        decimal voteAverage,
        int voteCount,
        string language,
        decimal popularity = 0) =>
        new(
            ExternalId: $"fake-tv-{tmdbId}",
            TmdbId: tmdbId,
            TvdbId: null,
            ImdbId: $"tt{tmdbId}",
            Title: title,
            OriginalTitle: title,
            Overview: $"Overview for {title}.",
            FirstAirDate: firstAirDate,
            PosterPath: $"/fake/discover-tv-{tmdbId}.jpg",
            BackdropPath: $"/fake/discover-tv-{tmdbId}-backdrop.jpg",
            OriginalLanguage: language,
            VoteAverage: voteAverage,
            VoteCount: voteCount,
            Popularity: popularity);
}
