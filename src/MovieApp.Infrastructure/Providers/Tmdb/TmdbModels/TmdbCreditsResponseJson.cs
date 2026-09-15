using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbCreditsResponseJson
{
    [JsonPropertyName("cast")]
    public List<TmdbCastMemberJson> Cast { get; init; } = [];

    [JsonPropertyName("crew")]
    public List<TmdbCrewMemberJson> Crew { get; init; } = [];
}

internal sealed class TmdbCastMemberJson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("character")]
    public string? Character { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }
}

internal sealed class TmdbCrewMemberJson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; init; }

    [JsonPropertyName("job")]
    public string? Job { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }
}
