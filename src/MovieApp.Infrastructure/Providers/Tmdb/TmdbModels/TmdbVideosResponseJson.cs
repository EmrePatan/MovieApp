namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

public sealed class TmdbVideosResponseJson
{
    public int Id { get; set; }

    public List<TmdbVideoJson> Results { get; set; } = [];
}

public sealed class TmdbVideoJson
{
    public string? Iso6391 { get; set; }

    public string? Iso31661 { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Site { get; set; } = string.Empty;

    public int Size { get; set; }

    public string Type { get; set; } = string.Empty;

    public bool Official { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
