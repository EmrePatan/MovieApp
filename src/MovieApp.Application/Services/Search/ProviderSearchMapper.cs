using MovieApp.Application.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal readonly record struct AutocompleteCatalogTarget(string Type, int TmdbId);

internal static class ProviderSearchMapper
{
    public static SearchItem ToSearchItem(MovieProviderSummary summary, Guid id) =>
        new(
            id,
            "movie",
            summary.Title,
            summary.OriginalTitle,
            summary.Overview,
            summary.PosterPath,
            null,
            summary.ReleaseDate,
            summary.VoteAverage,
            summary.VoteCount,
            summary.ReleaseDate?.Year,
            summary.TmdbId,
            Popularity: summary.Popularity);

    public static SearchItem ToSearchItem(TvShowProviderSummary summary, Guid id) =>
        new(
            id,
            "tv",
            summary.Title,
            summary.OriginalTitle,
            summary.Overview,
            summary.PosterPath,
            summary.BackdropPath,
            summary.FirstAirDate,
            summary.VoteAverage,
            summary.VoteCount,
            summary.FirstAirDate?.Year,
            summary.TmdbId,
            Popularity: summary.Popularity);

    public static SearchItem ToSearchItem(PersonProviderSummary summary, Guid id) =>
        new(
            id,
            "person",
            summary.Name,
            null,
            null,
            summary.ProfilePath,
            null,
            null,
            summary.Popularity,
            0,
            null,
            summary.TmdbId,
            summary.KnownForDepartment,
            summary.Popularity);

    public static SearchSuggestion ToSuggestion(SearchItem item) =>
        new(item.Id, item.Type, item.Title, item.PosterUrl, item.TmdbId, item.KnownForDepartment);

    public static PaginatedResult<SearchItem> MergeProviderResults(
        SearchCriteria criteria,
        MovieProviderSearchResult? movieResult,
        TvShowProviderSearchResult? tvResult,
        PersonProviderSearchResult? personResult,
        IReadOnlyDictionary<int, Guid> movieIds,
        IReadOnlyDictionary<int, Guid> tvIds,
        IReadOnlyDictionary<int, Guid> personIds)
    {
        var items = new List<SearchItem>();

        if (movieResult is not null)
        {
            foreach (var summary in movieResult.Results)
            {
                if (summary.TmdbId is null ||
                    !movieIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        if (tvResult is not null)
        {
            foreach (var summary in tvResult.Results)
            {
                if (summary.TmdbId is null ||
                    !tvIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        if (personResult is not null)
        {
            foreach (var summary in personResult.Results)
            {
                if (!personIds.TryGetValue(summary.TmdbId, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
            ? null
            : QueryNormalizer.Normalize(criteria.Query);

        var sortedItems = ApplyRelevanceSort(
            items,
            normalizedQuery,
            preferCatalogContent: criteria.Type == SearchContentType.All);

        return criteria.Type switch
        {
            SearchContentType.Movie => CreatePaginatedResult(
                sortedItems,
                movieResult?.Page ?? criteria.Page,
                movieResult?.PageSize ?? criteria.PageSize,
                movieResult?.TotalCount ?? 0),
            SearchContentType.Tv => CreatePaginatedResult(
                sortedItems,
                tvResult?.Page ?? criteria.Page,
                tvResult?.PageSize ?? criteria.PageSize,
                tvResult?.TotalCount ?? 0),
            SearchContentType.Person => CreatePaginatedResult(
                sortedItems,
                personResult?.Page ?? criteria.Page,
                personResult?.PageSize ?? criteria.PageSize,
                personResult?.TotalCount ?? 0),
            _ => CreateAllPaginatedResult(
                sortedItems,
                criteria,
                movieResult?.TotalCount ?? 0,
                tvResult?.TotalCount ?? 0,
                personResult?.TotalCount ?? 0)
        };
    }

    public static IReadOnlyList<AutocompleteCatalogTarget> SelectAutocompleteCatalogTargets(
        string query,
        MovieProviderSearchResult movieResult,
        TvShowProviderSearchResult tvResult,
        PersonProviderSearchResult personResult,
        int limit)
    {
        var rankingItems = new List<SearchItem>();

        foreach (var summary in movieResult.Results)
        {
            if (summary.TmdbId is null)
            {
                continue;
            }

            rankingItems.Add(ToRankingSearchItem(
                "movie",
                summary.Title,
                summary.OriginalTitle,
                summary.VoteAverage,
                summary.VoteCount,
                summary.TmdbId,
                popularity: summary.Popularity));
        }

        foreach (var summary in tvResult.Results)
        {
            if (summary.TmdbId is null)
            {
                continue;
            }

            rankingItems.Add(ToRankingSearchItem(
                "tv",
                summary.Title,
                summary.OriginalTitle,
                summary.VoteAverage,
                summary.VoteCount,
                summary.TmdbId,
                popularity: summary.Popularity));
        }

        foreach (var summary in personResult.Results)
        {
            rankingItems.Add(ToRankingSearchItem(
                "person",
                summary.Name,
                null,
                summary.Popularity,
                0,
                summary.TmdbId,
                summary.KnownForDepartment));
        }

        var normalizedQuery = QueryNormalizer.Normalize(query);

        return ApplyRelevanceSort(rankingItems, normalizedQuery, preferCatalogContent: true)
            .Take(limit)
            .Select(item => new AutocompleteCatalogTarget(item.Type, item.TmdbId!.Value))
            .ToList();
    }

    public static IReadOnlyList<MovieProviderSummary> SelectMovieIngestSummariesForTargets(
        IReadOnlyList<AutocompleteCatalogTarget> targets,
        MovieProviderSearchResult ingestResult)
    {
        var targetIds = targets
            .Where(target => target.Type == "movie")
            .Select(target => target.TmdbId)
            .ToHashSet();

        if (targetIds.Count == 0)
        {
            return [];
        }

        return ingestResult.Results
            .Where(summary => summary.TmdbId.HasValue && targetIds.Contains(summary.TmdbId.Value))
            .ToList();
    }

    public static IReadOnlyList<TvShowProviderSummary> SelectTvIngestSummariesForTargets(
        IReadOnlyList<AutocompleteCatalogTarget> targets,
        TvShowProviderSearchResult ingestResult)
    {
        var targetIds = targets
            .Where(target => target.Type == "tv")
            .Select(target => target.TmdbId)
            .ToHashSet();

        if (targetIds.Count == 0)
        {
            return [];
        }

        return ingestResult.Results
            .Where(summary => summary.TmdbId.HasValue && targetIds.Contains(summary.TmdbId.Value))
            .ToList();
    }

    public static IReadOnlyList<PersonProviderSummary> SelectPersonIngestSummariesForTargets(
        IReadOnlyList<AutocompleteCatalogTarget> targets,
        PersonProviderSearchResult ingestResult)
    {
        var targetIds = targets
            .Where(target => target.Type == "person")
            .Select(target => target.TmdbId)
            .ToHashSet();

        if (targetIds.Count == 0)
        {
            return [];
        }

        return ingestResult.Results
            .Where(summary => targetIds.Contains(summary.TmdbId))
            .ToList();
    }

    public static IReadOnlyList<SearchSuggestion> MergeAutocompleteSuggestions(
        string query,
        MovieProviderSearchResult? movieResult,
        TvShowProviderSearchResult? tvResult,
        PersonProviderSearchResult? personResult,
        IReadOnlyDictionary<int, Guid> movieIds,
        IReadOnlyDictionary<int, Guid> tvIds,
        IReadOnlyDictionary<int, Guid> personIds,
        int limit)
    {
        var items = new List<SearchItem>();

        if (movieResult is not null)
        {
            foreach (var summary in movieResult.Results)
            {
                if (summary.TmdbId is null ||
                    !movieIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        if (tvResult is not null)
        {
            foreach (var summary in tvResult.Results)
            {
                if (summary.TmdbId is null ||
                    !tvIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        if (personResult is not null)
        {
            foreach (var summary in personResult.Results)
            {
                if (!personIds.TryGetValue(summary.TmdbId, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        var normalizedQuery = QueryNormalizer.Normalize(query);

        return ApplyRelevanceSort(items, normalizedQuery, preferCatalogContent: true)
            .Take(limit)
            .Select(ToSuggestion)
            .ToList();
    }

    private static PaginatedResult<SearchItem> CreateAllPaginatedResult(
        IReadOnlyList<SearchItem> sortedItems,
        SearchCriteria criteria,
        int movieTotalCount,
        int tvTotalCount,
        int personTotalCount)
    {
        var personLimit = criteria.Page == 1 ? PersonSearchDefaults.MaxMixedResults : 0;
        var limitedItems = LimitMixedPersonResults(sortedItems, personLimit)
            .Take(criteria.PageSize)
            .ToList();

        var personContribution = criteria.Page == 1
            ? Math.Min(personTotalCount, PersonSearchDefaults.MaxMixedResults)
            : 0;

        return CreatePaginatedResult(
            limitedItems,
            criteria.Page,
            criteria.PageSize,
            movieTotalCount + tvTotalCount + personContribution);
    }

    private static IEnumerable<SearchItem> LimitMixedPersonResults(
        IReadOnlyList<SearchItem> sortedItems,
        int personLimit)
    {
        var personCount = 0;

        foreach (var item in sortedItems)
        {
            if (item.Type != "person")
            {
                yield return item;
                continue;
            }

            if (personCount >= personLimit)
            {
                continue;
            }

            personCount++;
            yield return item;
        }
    }

    private static PaginatedResult<SearchItem> CreatePaginatedResult(
        IReadOnlyList<SearchItem> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchItem>(
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static SearchItem ToRankingSearchItem(
        string type,
        string title,
        string? originalTitle,
        decimal voteAverage,
        int voteCount,
        int? tmdbId,
        string? knownForDepartment = null,
        decimal popularity = 0) =>
        new(
            Guid.Empty,
            type,
            title,
            originalTitle,
            null,
            null,
            null,
            null,
            voteAverage,
            voteCount,
            null,
            tmdbId,
            knownForDepartment,
            popularity);

    private static List<SearchItem> ApplyRelevanceSort(
        IReadOnlyList<SearchItem> items,
        string? normalizedQuery,
        bool preferCatalogContent = false)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return items
                .OrderByDescending(GetSearchRankingPopularity)
                .ThenByDescending(item => item.VoteCount)
                .ThenByDescending(item => item.VoteAverage)
                .ToList();
        }

        var context = BuildRankingContext(items, normalizedQuery, preferCatalogContent);

        return items
            .OrderBy(item => ComputeRelevanceRank(item, normalizedQuery, context))
            .ThenByDescending(GetSearchRankingPopularity)
            .ThenByDescending(item => item.VoteCount)
            .ThenByDescending(item => item.VoteAverage)
            .ToList();
    }

    private static decimal GetSearchRankingPopularity(SearchItem item) =>
        item.Popularity > 0 ? item.Popularity : item.VoteAverage;

    private readonly record struct RankingContext(
        bool PreferCatalogContent,
        bool CatalogFranchiseSignal,
        bool CatalogExactMatch,
        int QueryTokenCount);

    private static RankingContext BuildRankingContext(
        IReadOnlyList<SearchItem> items,
        string normalizedQuery,
        bool preferCatalogContent)
    {
        if (!preferCatalogContent)
        {
            return new RankingContext(false, false, false, CountQueryTokens(normalizedQuery));
        }

        var catalogFranchiseSignal = false;
        var catalogExactMatch = false;

        foreach (var item in items)
        {
            if (item.Type is not ("movie" or "tv"))
            {
                continue;
            }

            if (IsCatalogFranchiseMatch(item.Title, item.OriginalTitle, normalizedQuery))
            {
                catalogFranchiseSignal = true;
            }

            if (GetTextMatchTier(item.Title, item.OriginalTitle, normalizedQuery) == 0)
            {
                catalogExactMatch = true;
            }
        }

        return new RankingContext(
            preferCatalogContent,
            catalogFranchiseSignal,
            catalogExactMatch,
            CountQueryTokens(normalizedQuery));
    }

    private static int ComputeRelevanceRank(
        SearchItem item,
        string normalizedQuery,
        RankingContext context)
    {
        var tier = GetTextMatchTier(item.Title, item.OriginalTitle, normalizedQuery);

        if (!context.PreferCatalogContent || item.Type != "person")
        {
            return tier;
        }

        var exactPersonNameMatch = tier == 0;

        if (exactPersonNameMatch)
        {
            if (context.CatalogFranchiseSignal)
            {
                return 2;
            }

            if (context.QueryTokenCount == 1 && context.CatalogExactMatch)
            {
                return 2;
            }

            return 0;
        }

        return Math.Min(tier + 1, 3);
    }

    private static int GetTextMatchTier(string title, string? alternateTitle, string normalizedQuery)
    {
        var bestTier = 3;

        foreach (var normalizedTitle in EnumerateNormalizedNames(title, alternateTitle))
        {
            if (normalizedTitle == normalizedQuery)
            {
                bestTier = Math.Min(bestTier, 0);
                continue;
            }

            if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                bestTier = Math.Min(bestTier, 1);
                continue;
            }

            if (HasTokenMatch(normalizedTitle, normalizedQuery))
            {
                bestTier = Math.Min(bestTier, 2);
            }
        }

        return bestTier;
    }

    private static bool IsCatalogFranchiseMatch(string title, string? alternateTitle, string normalizedQuery)
    {
        foreach (var normalizedTitle in EnumerateNormalizedNames(title, alternateTitle))
        {
            if (normalizedTitle == normalizedQuery)
            {
                return true;
            }

            if (normalizedTitle.StartsWith(normalizedQuery + " and ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateNormalizedNames(string title, string? alternateTitle)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            yield return QueryNormalizer.Normalize(title);
        }

        if (!string.IsNullOrWhiteSpace(alternateTitle))
        {
            yield return QueryNormalizer.Normalize(alternateTitle);
        }
    }

    private static bool HasTokenMatch(string normalizedTitle, string normalizedQuery)
    {
        var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryTokens.Length == 0)
        {
            return false;
        }

        return queryTokens.All(token => normalizedTitle.Contains(token, StringComparison.Ordinal));
    }

    private static int CountQueryTokens(string normalizedQuery) =>
        normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
