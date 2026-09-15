using MovieApp.Application.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class ProviderSearchMapper
{
    public static SearchItem ToSearchItem(MovieProviderSummary summary, Guid id) =>
        new(
            id,
            "movie",
            summary.Title,
            null,
            summary.Overview,
            summary.PosterPath,
            null,
            summary.ReleaseDate,
            summary.VoteAverage,
            summary.VoteCount,
            summary.ReleaseDate?.Year,
            summary.TmdbId);

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
            summary.TmdbId);

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
            summary.KnownForDepartment);

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

        var sortedItems = ApplyRelevanceSort(items, normalizedQuery);

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

        return ApplyRelevanceSort(items, normalizedQuery)
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

    private static List<SearchItem> ApplyRelevanceSort(
        IReadOnlyList<SearchItem> items,
        string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return items
                .OrderByDescending(item => item.VoteAverage)
                .ThenByDescending(item => item.VoteCount)
                .ToList();
        }

        return items
            .OrderBy(item => ComputeRelevanceRank(item.Title, normalizedQuery))
            .ThenByDescending(item => item.VoteAverage)
            .ThenByDescending(item => item.VoteCount)
            .ToList();
    }

    private static int ComputeRelevanceRank(string title, string normalizedQuery)
    {
        var normalizedTitle = QueryNormalizer.Normalize(title);

        if (string.Equals(normalizedTitle, normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}
