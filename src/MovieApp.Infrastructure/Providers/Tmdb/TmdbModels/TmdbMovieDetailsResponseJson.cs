namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbMovieDetailsResponseJson
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public string? ReleaseDate { get; set; }

    public int? Runtime { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public string? OriginalLanguage { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public List<TmdbGenreJson> Genres { get; set; } = [];

    public string? ImdbId { get; set; }

    public TmdbExternalIdsJson? ExternalIds { get; set; }
}
