namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbBelongsToCollectionJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }
}
