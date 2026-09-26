using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchTitleFilter
{
    public static IQueryable<Movie> WhereMovieTitleContains(IQueryable<Movie> query, SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var primary = match.Primary;
        var turkish = match.TurkishAlternate;
        return query.Where(movie =>
            EF.Functions.ILike(movie.Title, $"%{primary}%")
            || (turkish != null && EF.Functions.ILike(movie.Title, $"%{turkish}%"))
            || (movie.OriginalTitle != null && EF.Functions.ILike(movie.OriginalTitle, $"%{primary}%"))
            || (turkish != null
                && movie.OriginalTitle != null
                && EF.Functions.ILike(movie.OriginalTitle, $"%{turkish}%")));
    }

    public static IQueryable<TvShow> WhereTvShowTitleContains(IQueryable<TvShow> query, SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var primary = match.Primary;
        var turkish = match.TurkishAlternate;
        return query.Where(tvShow =>
            EF.Functions.ILike(tvShow.Title, $"%{primary}%")
            || (turkish != null && EF.Functions.ILike(tvShow.Title, $"%{turkish}%"))
            || (tvShow.OriginalTitle != null && EF.Functions.ILike(tvShow.OriginalTitle, $"%{primary}%"))
            || (turkish != null
                && tvShow.OriginalTitle != null
                && EF.Functions.ILike(tvShow.OriginalTitle, $"%{turkish}%")));
    }

    public static IQueryable<Person> WherePersonNameContains(IQueryable<Person> query, SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var primary = match.Primary;
        var turkish = match.TurkishAlternate;
        return query.Where(person =>
            EF.Functions.ILike(person.Name, $"%{primary}%")
            || (turkish != null && EF.Functions.ILike(person.Name, $"%{turkish}%")));
    }

    public static IOrderedQueryable<SearchItemProjection> OrderByRelevance(
        IQueryable<SearchItemProjection> query,
        SearchTextMatch match)
    {
        var primary = match.Primary;
        var turkish = match.TurkishAlternate;

        return query.OrderBy(item =>
            (EF.Functions.ILike(item.Title, primary) && item.Title.Length == primary.Length)
            || (turkish != null
                && EF.Functions.ILike(item.Title, turkish)
                && item.Title.Length == turkish.Length)
                ? 0
                : EF.Functions.ILike(item.Title, primary + "%")
                    || (turkish != null && EF.Functions.ILike(item.Title, turkish + "%"))
                    ? 1
                    : 2);
    }

    public static IQueryable<SearchItemProjection> WhereAfterRelevanceCursor(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor,
        SearchTextMatch match)
    {
        var primary = match.Primary;
        var turkish = match.TurkishAlternate;

        return query.Where(item =>
            RelevanceTier(item.Title, primary, turkish) > cursor.RelevanceTier
            || (RelevanceTier(item.Title, primary, turkish) == cursor.RelevanceTier
                && item.VoteCount < cursor.VoteCount)
            || (RelevanceTier(item.Title, primary, turkish) == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage < cursor.VoteAverage)
            || (RelevanceTier(item.Title, primary, turkish) == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type.CompareTo(cursor.Type) > 0)
            || (RelevanceTier(item.Title, primary, turkish) == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));
    }

    private static int RelevanceTier(string title, string primary, string? turkish) =>
        (EF.Functions.ILike(title, primary) && title.Length == primary.Length)
        || (turkish != null && EF.Functions.ILike(title, turkish) && title.Length == turkish.Length)
            ? 0
            : EF.Functions.ILike(title, primary + "%")
                || (turkish != null && EF.Functions.ILike(title, turkish + "%"))
                ? 1
                : 2;
}
