using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbWatchProvidersResponseJson
{
    [JsonPropertyName("results")]
    public Dictionary<string, TmdbWatchProviderRegionJson> Results { get; init; } = [];
}

internal sealed class TmdbWatchProviderRegionJson
{
    [JsonPropertyName("link")]
    public string? Link { get; init; }

    [JsonPropertyName("flatrate")]
    public List<TmdbWatchProviderJson>? Flatrate { get; init; }

    [JsonPropertyName("rent")]
    public List<TmdbWatchProviderJson>? Rent { get; init; }

    [JsonPropertyName("buy")]
    public List<TmdbWatchProviderJson>? Buy { get; init; }
}

internal sealed class TmdbWatchProviderJson
{
    [JsonPropertyName("provider_id")]
    public int ProviderId { get; init; }

    [JsonPropertyName("provider_name")]
    public string ProviderName { get; init; } = string.Empty;

    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; init; }

    [JsonPropertyName("display_priority")]
    public int DisplayPriority { get; init; }
}
