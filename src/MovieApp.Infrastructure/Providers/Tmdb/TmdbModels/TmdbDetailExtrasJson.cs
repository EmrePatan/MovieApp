using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

public sealed class TmdbNamedJson
{
    public string? Name { get; set; }
}

public sealed class TmdbContentRatingsJson
{
    public List<TmdbContentRatingJson> Results { get; set; } = [];
}

public sealed class TmdbContentRatingJson
{
    // The snake_case naming policy does not produce this TMDB name.
    [JsonPropertyName("iso_3166_1")]
    public string? Iso31661 { get; set; }

    public string? Rating { get; set; }
}

public sealed class TmdbCertificationReleasesJson
{
    public List<TmdbCountryCertificationsJson> Results { get; set; } = [];
}

public sealed class TmdbCountryCertificationsJson
{
    [JsonPropertyName("iso_3166_1")]
    public string? Iso31661 { get; set; }

    public List<TmdbReleaseDateEntryJson> ReleaseDates { get; set; } = [];
}
