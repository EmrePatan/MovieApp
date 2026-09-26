#pragma warning disable CA1309, CA2251

using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Search;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchKeysetPagination
{
    public static IQueryable<SearchItemProjection> ApplyAfterCursor(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor,
        SearchSortOption sort,
        string? normalizedQuery)
    {
        var effectiveSort = sort == SearchSortOption.Rating ? SearchSortOption.RatingDesc : sort;

        return effectiveSort switch
        {
            SearchSortOption.RatingDesc => ApplyAfterRatingDesc(query, cursor),
            SearchSortOption.RatingAsc => ApplyAfterRatingAsc(query, cursor),
            SearchSortOption.DateDesc => ApplyAfterDateDesc(query, cursor),
            SearchSortOption.DateAsc => ApplyAfterDateAsc(query, cursor),
            SearchSortOption.TitleAsc => ApplyAfterTitleAsc(query, cursor),
            SearchSortOption.TitleDesc => ApplyAfterTitleDesc(query, cursor),
            SearchSortOption.Popular => ApplyAfterPopular(query, cursor),
            _ => ApplyAfterRelevance(query, cursor, normalizedQuery)
        };
    }

    private static IQueryable<SearchItemProjection> ApplyAfterRatingDesc(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor) =>
        query.Where(item =>
            item.VoteAverage < cursor.VoteAverage
            || (item.VoteAverage == cursor.VoteAverage && item.VoteCount < cursor.VoteCount)
            || (item.VoteAverage == cursor.VoteAverage
                && item.VoteCount == cursor.VoteCount
                && item.Type.CompareTo(cursor.Type) > 0)
            || (item.VoteAverage == cursor.VoteAverage
                && item.VoteCount == cursor.VoteCount
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));

    private static IQueryable<SearchItemProjection> ApplyAfterRatingAsc(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor) =>
        query.Where(item =>
            item.VoteAverage > cursor.VoteAverage
            || (item.VoteAverage == cursor.VoteAverage && item.VoteCount > cursor.VoteCount)
            || (item.VoteAverage == cursor.VoteAverage
                && item.VoteCount == cursor.VoteCount
                && item.Type.CompareTo(cursor.Type) > 0)
            || (item.VoteAverage == cursor.VoteAverage
                && item.VoteCount == cursor.VoteCount
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));

    private static IQueryable<SearchItemProjection> ApplyAfterPopular(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor) =>
        query.Where(item =>
            item.VoteCount < cursor.VoteCount
            || (item.VoteCount == cursor.VoteCount && item.VoteAverage < cursor.VoteAverage)
            || (item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type.CompareTo(cursor.Type) > 0)
            || (item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));

    private static IQueryable<SearchItemProjection> ApplyAfterTitleAsc(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor) =>
        query.Where(item =>
            item.Title.CompareTo(cursor.Title) > 0
            || (item.Title == cursor.Title && item.Type.CompareTo(cursor.Type) > 0)
            || (item.Title == cursor.Title && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));

    private static IQueryable<SearchItemProjection> ApplyAfterTitleDesc(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor) =>
        query.Where(item =>
            item.Title.CompareTo(cursor.Title) < 0
            || (item.Title == cursor.Title && item.Type.CompareTo(cursor.Type) > 0)
            || (item.Title == cursor.Title && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));

    private static IQueryable<SearchItemProjection> ApplyAfterDateDesc(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor)
    {
        var anchorDate = ParseReleaseDate(cursor.ReleaseDateIso);
        return query.Where(item =>
            (item.ReleaseDate ?? DateOnly.MinValue) < anchorDate
            || ((item.ReleaseDate ?? DateOnly.MinValue) == anchorDate && item.VoteAverage < cursor.VoteAverage)
            || ((item.ReleaseDate ?? DateOnly.MinValue) == anchorDate
                && item.VoteAverage == cursor.VoteAverage
                && item.Type.CompareTo(cursor.Type) > 0)
            || ((item.ReleaseDate ?? DateOnly.MinValue) == anchorDate
                && item.VoteAverage == cursor.VoteAverage
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));
    }

    private static IQueryable<SearchItemProjection> ApplyAfterDateAsc(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor)
    {
        var anchorDate = ParseReleaseDate(cursor.ReleaseDateIso);
        return query.Where(item =>
            (item.ReleaseDate ?? DateOnly.MaxValue) > anchorDate
            || ((item.ReleaseDate ?? DateOnly.MaxValue) == anchorDate && item.VoteAverage > cursor.VoteAverage)
            || ((item.ReleaseDate ?? DateOnly.MaxValue) == anchorDate
                && item.VoteAverage == cursor.VoteAverage
                && item.Type.CompareTo(cursor.Type) > 0)
            || ((item.ReleaseDate ?? DateOnly.MaxValue) == anchorDate
                && item.VoteAverage == cursor.VoteAverage
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));
    }

    private static IQueryable<SearchItemProjection> ApplyAfterRelevance(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor,
        string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return ApplyAfterPopular(query, cursor);
        }

        return query.Where(item =>
            (EF.Functions.ILike(item.Title, normalizedQuery) && item.Title.Length == normalizedQuery.Length
                ? 0
                : EF.Functions.ILike(item.Title, normalizedQuery + "%")
                    ? 1
                    : 2) > cursor.RelevanceTier
            || ((EF.Functions.ILike(item.Title, normalizedQuery) && item.Title.Length == normalizedQuery.Length
                    ? 0
                    : EF.Functions.ILike(item.Title, normalizedQuery + "%")
                        ? 1
                        : 2) == cursor.RelevanceTier
                && item.VoteCount < cursor.VoteCount)
            || ((EF.Functions.ILike(item.Title, normalizedQuery) && item.Title.Length == normalizedQuery.Length
                    ? 0
                    : EF.Functions.ILike(item.Title, normalizedQuery + "%")
                        ? 1
                        : 2) == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage < cursor.VoteAverage)
            || ((EF.Functions.ILike(item.Title, normalizedQuery) && item.Title.Length == normalizedQuery.Length
                    ? 0
                    : EF.Functions.ILike(item.Title, normalizedQuery + "%")
                        ? 1
                        : 2) == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type.CompareTo(cursor.Type) > 0)
            || ((EF.Functions.ILike(item.Title, normalizedQuery) && item.Title.Length == normalizedQuery.Length
                    ? 0
                    : EF.Functions.ILike(item.Title, normalizedQuery + "%")
                        ? 1
                        : 2) == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));
    }

    private static DateOnly ParseReleaseDate(string? releaseDateIso) =>
        string.IsNullOrWhiteSpace(releaseDateIso)
            ? DateOnly.MinValue
            : DateOnly.Parse(releaseDateIso, System.Globalization.CultureInfo.InvariantCulture);
}
