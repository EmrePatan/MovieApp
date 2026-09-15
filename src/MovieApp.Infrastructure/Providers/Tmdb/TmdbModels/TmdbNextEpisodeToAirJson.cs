using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbNextEpisodeToAirJson
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; set; }

    [JsonPropertyName("episode_number")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; set; }
}
