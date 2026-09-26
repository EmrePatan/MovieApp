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
        SearchQueryMatch match) =>
        query.OrderBy(item => item.RelevanceTier);

    public static IQueryable<SearchItemProjection> WhereAfterRelevanceCursor(
        IQueryable<SearchItemProjection> query,
        SearchKeysetCursor cursor,
        SearchQueryMatch match) =>
        query.Where(item =>
            item.RelevanceTier > cursor.RelevanceTier
            || (item.RelevanceTier == cursor.RelevanceTier
                && item.VoteCount < cursor.VoteCount)
            || (item.RelevanceTier == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage < cursor.VoteAverage)
            || (item.RelevanceTier == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type.CompareTo(cursor.Type) > 0)
            || (item.RelevanceTier == cursor.RelevanceTier
                && item.VoteCount == cursor.VoteCount
                && item.VoteAverage == cursor.VoteAverage
                && item.Type == cursor.Type
                && item.Id.CompareTo(cursor.Id) > 0));

}
