using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

/// <summary>
/// Serializes follow/block transitions for the same unordered user pair.
/// Must be called inside an active database transaction.
/// </summary>
internal static class SocialPairLock
{
    public static Task AcquireAsync(
        ApplicationDbContext dbContext,
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken)
    {
        var first = firstUserId.CompareTo(secondUserId) <= 0 ? firstUserId : secondUserId;
        var second = firstUserId.CompareTo(secondUserId) <= 0 ? secondUserId : firstUserId;
        var lockKey = $"{first:N}:{second:N}";

        // A transaction-scoped advisory lock makes Follow, Accept and Block
        // observe a single serialized order for this user pair.
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }
}
