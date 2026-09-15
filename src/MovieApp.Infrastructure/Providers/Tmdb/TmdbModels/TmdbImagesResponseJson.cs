namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbImagesResponseJson
{
    public int Id { get; set; }

    public List<TmdbImageJson> Backdrops { get; set; } = [];

    public List<TmdbImageJson> Posters { get; set; } = [];

    public List<TmdbImageJson> Logos { get; set; } = [];

    public List<TmdbImageJson> Profiles { get; set; } = [];
}

internal sealed class TmdbImageJson
{
    public string? FilePath { get; set; }

    public string? Iso6391 { get; set; }

    public double AspectRatio { get; set; }

    public int Height { get; set; }

    public int Width { get; set; }

    public double VoteAverage { get; set; }

    public int VoteCount { get; set; }
}
