using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class LibrarySearchTitleFilter
{
    public static IQueryable<Movie> WhereMovieMatches(
        ApplicationDbContext dbContext,
        IQueryable<Movie> query,
        SearchTextMatch match,
        string? titleMatchContentLocale = null)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var scope = ResolveScope(titleMatchContentLocale);

        return SearchCatalogContentQuery.WhereMovieMatchesSearch(
            query,
            dbContext,
            SearchQueryMatch.From(match),
            scope);
    }

    public static IQueryable<TvShow> WhereTvShowMatches(
        ApplicationDbContext dbContext,
        IQueryable<TvShow> query,
        SearchTextMatch match,
        string? titleMatchContentLocale = null)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var scope = ResolveScope(titleMatchContentLocale);

        return SearchCatalogContentQuery.WhereTvShowMatchesSearch(
            query,
            dbContext,
            SearchQueryMatch.From(match),
            scope);
    }

    public static IQueryable<Guid> MatchingMovieIds(
        ApplicationDbContext dbContext,
        SearchTextMatch match,
        string? titleMatchContentLocale = null) =>
        WhereMovieMatches(dbContext, dbContext.Movies.AsNoTracking(), match, titleMatchContentLocale)
            .Select(movie => movie.Id);

    public static IQueryable<Guid> MatchingTvShowIds(
        ApplicationDbContext dbContext,
        SearchTextMatch match,
        string? titleMatchContentLocale = null) =>
        WhereTvShowMatches(dbContext, dbContext.TvShows.AsNoTracking(), match, titleMatchContentLocale)
            .Select(tvShow => tvShow.Id);

    private static CatalogSearchTitleLanguageScope ResolveScope(string? titleMatchContentLocale) =>
        string.IsNullOrWhiteSpace(titleMatchContentLocale)
            ? CatalogSearchTitleLanguageScope.FromContentLocale(ContentLocaleResolver.EnglishUnitedStates)
            : CatalogSearchTitleLanguageScope.FromContentLocale(titleMatchContentLocale);
}
