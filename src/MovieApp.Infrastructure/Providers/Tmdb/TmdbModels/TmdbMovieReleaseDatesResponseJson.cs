namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

public sealed class TmdbMovieReleaseDatesResponseJson
{
    public int Id { get; set; }

    public List<TmdbRegionalReleaseDatesJson> Results { get; set; } = [];
}

public sealed class TmdbRegionalReleaseDatesJson
{
    public string Iso31661 { get; set; } = string.Empty;

    public List<TmdbReleaseDateEntryJson> ReleaseDates { get; set; } = [];
}

public sealed class TmdbReleaseDateEntryJson
{
    public string Certification { get; set; } = string.Empty;

    public string? Iso6391 { get; set; }

    public string? Note { get; set; }

    public string ReleaseDate { get; set; } = string.Empty;

    public int Type { get; set; }
}
