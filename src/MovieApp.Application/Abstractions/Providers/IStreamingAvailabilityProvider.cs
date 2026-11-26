using MovieApp.Application.Models.StreamingLeaving;

namespace MovieApp.Application.Abstractions.Providers;

/// <summary>Every call to this provider costs one request of the monthly quota.</summary>
public interface IStreamingAvailabilityProvider
{
    /// <summary>Subscription services of a country. Empty when the service has no data for the country.</summary>
    Task<IReadOnlyList<StreamingService>> GetSubscriptionServicesAsync(
        string region,
        CancellationToken cancellationToken = default);

    /// <summary>One page of titles leaving the given services, nearest date first.</summary>
    Task<StreamingLeavingPage> GetExpiringAsync(
        string region,
        IReadOnlyList<string> serviceIds,
        string? cursor,
        CancellationToken cancellationToken = default);
}

public interface IStreamingLeavingTitleResolver
{
    /// <summary>
    /// Maps TMDB titles to catalog ids, adding the missing ones to the catalog.
    /// Titles that cannot be resolved are left out of the result.
    /// </summary>
    Task<IReadOnlyDictionary<StreamingTitleKey, Guid>> ResolveAsync(
        IReadOnlyList<StreamingTitleKey> titles,
        CancellationToken cancellationToken = default);
}

public interface IStreamingLeavingFetchJobEnqueuer
{
    void EnqueueFetch(string region);
}
