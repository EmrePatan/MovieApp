using MovieApp.Application.Common;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class LibraryWatchedTitleFilter
{
    public static IQueryable<LibraryRepository.WatchedUnionRow> WhereTitleContains(
        ApplicationDbContext dbContext,
        IQueryable<LibraryRepository.WatchedUnionRow> rows,
        SearchTextMatch match,
        string? titleMatchContentLocale = null)
    {
        if (match.IsEmpty)
        {
            return rows;
        }

        var matchingMovieIds = LibrarySearchTitleFilter.MatchingMovieIds(
            dbContext,
            match,
            titleMatchContentLocale);
        var matchingTvShowIds = LibrarySearchTitleFilter.MatchingTvShowIds(
            dbContext,
            match,
            titleMatchContentLocale);

        return rows.Where(row =>
            (row.Type == "movie" && matchingMovieIds.Contains(row.Id))
            || (row.Type == "tv" && matchingTvShowIds.Contains(row.Id)));
    }
}
