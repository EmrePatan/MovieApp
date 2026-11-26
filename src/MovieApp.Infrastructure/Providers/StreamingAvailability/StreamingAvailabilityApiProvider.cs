using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.StreamingAvailability;

/// <summary>
/// Streaming Availability API client. The key is set as a default header on the HttpClient.
/// No automatic retry: every request counts against the monthly quota.
/// </summary>
public sealed class StreamingAvailabilityApiProvider(HttpClient httpClient) : IStreamingAvailabilityProvider
{
    private const string Subscription = "subscription";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<StreamingService>> GetSubscriptionServicesAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"countries/{Country(region)}",
            cancellationToken);

        // The service answers like this for a country it does not cover.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CountryBody>(JsonOptions, cancellationToken)
            ?? throw new JsonException("Empty country response.");

        return (body.Services ?? [])
            .Where(service => service.StreamingOptionTypes?.Subscription == true)
            .Select(service => new StreamingService(service.Id, service.Name ?? service.Id))
            .ToList();
    }

    public async Task<StreamingLeavingPage> GetExpiringAsync(
        string region,
        IReadOnlyList<string> serviceIds,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var catalogs = string.Join(',', serviceIds.Select(id => $"{id}.{Subscription}"));
        var path = $"changes?country={Country(region)}&change_type=expiring&item_type=show" +
            $"&include_unknown_dates=false&catalogs={Uri.EscapeDataString(catalogs)}";
        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        var body = await httpClient.GetFromJsonAsync<ChangesBody>(path, JsonOptions, cancellationToken)
            ?? throw new JsonException("Empty changes response.");

        var changes = new List<StreamingLeavingChange>();
        foreach (var change in body.Changes ?? [])
        {
            if (change.StreamingOptionType != Subscription ||
                change.Timestamp is not { } timestamp ||
                change.Service?.Id is not { } serviceId ||
                change.ShowId is null ||
                body.Shows is null ||
                !body.Shows.TryGetValue(change.ShowId, out var show) ||
                !TryParseTmdbId(show.TmdbId, out var title))
            {
                continue;
            }

            changes.Add(new StreamingLeavingChange(
                serviceId,
                title,
                DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime,
                change.Link));
        }

        return new StreamingLeavingPage(changes, body.HasMore ? body.NextCursor : null);
    }

    private static string Country(string region) => Uri.EscapeDataString(region.ToLowerInvariant());

    /// <summary>The service writes TMDB ids as "movie/654" or "tv/83054".</summary>
    private static bool TryParseTmdbId(string? value, out StreamingTitleKey title)
    {
        title = default;
        var parts = value?.Split('/');
        if (parts is not [var kind, var number] ||
            !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
            id <= 0)
        {
            return false;
        }

        switch (kind)
        {
            case "movie":
                title = new StreamingTitleKey(CatalogContentType.Movie, id);
                return true;
            case "tv":
                title = new StreamingTitleKey(CatalogContentType.Tv, id);
                return true;
            default:
                return false;
        }
    }

    private sealed record CountryBody(List<ServiceBody>? Services);

    private sealed record ServiceBody(string Id, string? Name, OptionTypesBody? StreamingOptionTypes);

    private sealed record OptionTypesBody(bool Subscription);

    private sealed record ChangesBody(
        List<ChangeBody>? Changes,
        Dictionary<string, ShowBody>? Shows,
        bool HasMore,
        string? NextCursor);

    private sealed record ChangeBody(
        string? ShowId,
        ChangeServiceBody? Service,
        string? StreamingOptionType,
        long? Timestamp,
        string? Link);

    private sealed record ChangeServiceBody(string? Id);

    private sealed record ShowBody(string? TmdbId);
}
