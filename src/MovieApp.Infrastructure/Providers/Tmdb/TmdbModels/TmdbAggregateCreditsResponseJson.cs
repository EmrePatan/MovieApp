using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbAggregateCreditsResponseJson
{
    [JsonPropertyName("cast")]
    public List<TmdbAggregateCastMemberJson> Cast { get; init; } = [];

    [JsonPropertyName("crew")]
    public List<TmdbAggregateCrewMemberJson> Crew { get; init; } = [];
}

internal sealed class TmdbAggregateCastMemberJson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }

    [JsonPropertyName("order")]
    public int Order { get; init; }

    [JsonPropertyName("total_episode_count")]
    public int TotalEpisodeCount { get; init; }

    [JsonPropertyName("roles")]
    public List<TmdbAggregateRoleJson> Roles { get; init; } = [];
}

internal sealed class TmdbAggregateRoleJson
{
    [JsonPropertyName("character")]
    public string? Character { get; init; }

    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; init; }
}

internal sealed class TmdbAggregateCrewMemberJson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; init; }

    [JsonPropertyName("profile_path")]
    public string? ProfilePath { get; init; }

    [JsonPropertyName("jobs")]
    public List<TmdbAggregateCrewJobJson> Jobs { get; init; } = [];
}

internal sealed class TmdbAggregateCrewJobJson
{
    [JsonPropertyName("job")]
    public string? Job { get; init; }

    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; init; }
}
