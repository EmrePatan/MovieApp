using Microsoft.EntityFrameworkCore;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class SingleRowQueryExtensions
{
    /// <summary>
    /// Reads a query whose SQL yields at most one row by construction (scalar-aggregate SELECTs,
    /// whole-set GROUP BY, explicit LIMIT 1). Unlike FirstOrDefault it adds no LIMIT, so EF does not
    /// warn about an unordered First, and a second row surfaces as an error instead of being dropped.
    /// </summary>
    internal static async Task<T?> SingleRowOrDefaultAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken)
    {
        var rows = await source.ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }
}
