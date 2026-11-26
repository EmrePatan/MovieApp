using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.StreamingAvailability;

/// <summary>Fixed list for local development without a key. Dates are relative to now.</summary>
public sealed class FakeStreamingAvailabilityProvider(TimeProvider timeProvider) : IStreamingAvailabilityProvider
{
    private static readonly (string Service, CatalogContentType Type, int TmdbId, int Days)[] Titles =
    [
        ("netflix", CatalogContentType.Movie, 157336, 1),
        ("netflix", CatalogContentType.Movie, 603, 2),
        ("netflix", CatalogContentType.Movie, 550, 3),
        ("prime", CatalogContentType.Movie, 603, 4),
        ("prime", CatalogContentType.Movie, 13, 5),
        ("netflix", CatalogContentType.Movie, 680, 8),
        ("prime", CatalogContentType.Movie, 27205, 12),
        ("netflix", CatalogContentType.Tv, 1396, 15),
        ("prime", CatalogContentType.Tv, 1399, 20)
    ];

    public Task<IReadOnlyList<StreamingService>> GetSubscriptionServicesAsync(
        string region,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StreamingService>>(
            [new StreamingService("netflix", "Netflix"), new StreamingService("prime", "Prime Video")]);

    public Task<StreamingLeavingPage> GetExpiringAsync(
        string region,
        IReadOnlyList<string> serviceIds,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var changes = Titles
            .Where(title => serviceIds.Contains(title.Service))
            .Select(title => new StreamingLeavingChange(
                title.Service,
                new StreamingTitleKey(title.Type, title.TmdbId),
                now.AddDays(title.Days),
                null))
            .ToList();
        return Task.FromResult(new StreamingLeavingPage(changes, null));
    }
}
