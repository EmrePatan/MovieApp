using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IStreamingLeavingRepository
{
    Task<StreamingLeavingRegion?> GetRegionAsync(string region, CancellationToken cancellationToken = default);

    Task SaveRegionAsync(StreamingLeavingRegion state, CancellationToken cancellationToken = default);

    /// <summary>Requests already spent in the period, per region.</summary>
    Task<IReadOnlyDictionary<string, int>> GetUsageAsync(
        DateOnly periodStart,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts one request for the region if <paramref name="isAllowed"/> accepts the current usage.
    /// Check and increment are atomic across regions and server instances.
    /// </summary>
    Task<bool> TryReserveRequestAsync(
        DateOnly periodStart,
        string region,
        Func<IReadOnlyDictionary<string, int>, bool> isAllowed,
        CancellationToken cancellationToken = default);

    /// <summary>Stored titles that have not left yet, with the catalog data needed to filter and show them.</summary>
    Task<IReadOnlyList<StreamingLeavingRow>> GetUpcomingAsync(
        string region,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> HasUpcomingAsync(string region, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Replaces all rows of the region in one transaction.</summary>
    Task ReplaceItemsAsync(
        string region,
        IReadOnlyList<StreamingLeavingItem> items,
        CancellationToken cancellationToken = default);
}
