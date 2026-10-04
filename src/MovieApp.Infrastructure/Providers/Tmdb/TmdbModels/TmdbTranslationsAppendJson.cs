using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTranslationsAppendJson
{
    public List<TmdbTranslationJson> Translations { get; set; } = [];
}

internal sealed class TmdbTranslationJson
{
    [JsonPropertyName("iso_3166_1")]
    public string? Iso31661 { get; set; }

    [JsonPropertyName("iso_639_1")]
    public string? Iso6391 { get; set; }

    public TmdbMovieTranslationDataJson? Data { get; set; }
}

internal sealed class TmdbMovieTranslationDataJson
{
    public string? Title { get; set; }
}

internal sealed class TmdbTvTranslationJson
{
    [JsonPropertyName("iso_3166_1")]
    public string? Iso31661 { get; set; }

    [JsonPropertyName("iso_639_1")]
    public string? Iso6391 { get; set; }

    public TmdbTvTranslationDataJson? Data { get; set; }
}

internal sealed class TmdbTvTranslationDataJson
{
    public string? Name { get; set; }
}

internal sealed class TmdbTvTranslationsAppendJson
{
    public List<TmdbTvTranslationJson> Translations { get; set; } = [];
}
