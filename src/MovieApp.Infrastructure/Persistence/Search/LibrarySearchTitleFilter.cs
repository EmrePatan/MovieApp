using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class LibrarySearchTitleFilter
{
    public static IQueryable<Movie> WhereMovieMatches(
        ApplicationDbContext dbContext,
        IQueryable<Movie> query,
        SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        return SearchCatalogContentQuery.WhereMovieMatchesSearch(
            query,
            dbContext,
            SearchQueryMatch.From(match));
    }

    public static IQueryable<TvShow> WhereTvShowMatches(
        ApplicationDbContext dbContext,
        IQueryable<TvShow> query,
        SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        return SearchCatalogContentQuery.WhereTvShowMatchesSearch(
            query,
            dbContext,
            SearchQueryMatch.From(match));
    }

    public static IQueryable<Guid> MatchingMovieIds(ApplicationDbContext dbContext, SearchTextMatch match) =>
        WhereMovieMatches(dbContext, dbContext.Movies.AsNoTracking(), match).Select(movie => movie.Id);

    public static IQueryable<Guid> MatchingTvShowIds(ApplicationDbContext dbContext, SearchTextMatch match) =>
        WhereTvShowMatches(dbContext, dbContext.TvShows.AsNoTracking(), match).Select(tvShow => tvShow.Id);
}
