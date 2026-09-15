using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbWatchProviderCatalogResponseJson
{
    [JsonPropertyName("results")]
    public List<TmdbWatchProviderCatalogItemJson> Results { get; set; } = [];
}

internal sealed class TmdbWatchProviderCatalogItemJson
{
    [JsonPropertyName("provider_id")]
    public int ProviderId { get; set; }

    [JsonPropertyName("provider_name")]
    public string ProviderName { get; set; } = string.Empty;

    [JsonPropertyName("logo_path")]
    public string? LogoPath { get; set; }

    [JsonPropertyName("display_priority")]
    public int DisplayPriority { get; set; }
}
