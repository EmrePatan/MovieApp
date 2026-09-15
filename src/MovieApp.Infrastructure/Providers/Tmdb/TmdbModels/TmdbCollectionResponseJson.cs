namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbCollectionResponseJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Overview { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public List<TmdbCollectionPartJson> Parts { get; set; } = [];
}

internal sealed class TmdbCollectionPartJson
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public string? ReleaseDate { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public bool Adult { get; set; }
}
