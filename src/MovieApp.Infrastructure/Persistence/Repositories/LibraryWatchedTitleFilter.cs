using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class LibraryWatchedTitleFilter
{
    public static IQueryable<LibraryRepository.WatchedUnionRow> WhereTitleContains(
        IQueryable<LibraryRepository.WatchedUnionRow> rows,
        SearchTextMatch match)
    {
        if (match.IsEmpty)
        {
            return rows;
        }

        var primary = match.Primary;
        var turkish = match.TurkishAlternate;
        return rows.Where(row =>
            EF.Functions.ILike(row.Title, $"%{primary}%")
            || (turkish != null && EF.Functions.ILike(row.Title, $"%{turkish}%"))
            || (row.OriginalTitle != null && EF.Functions.ILike(row.OriginalTitle, $"%{primary}%"))
            || (turkish != null
                && row.OriginalTitle != null
                && EF.Functions.ILike(row.OriginalTitle, $"%{turkish}%")));
    }
}
