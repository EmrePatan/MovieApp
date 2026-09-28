using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbProductionCountryJson
{
    [JsonPropertyName("iso_3166_1")]
    public string? Iso31661 { get; set; }

    public string? Name { get; set; }
}
