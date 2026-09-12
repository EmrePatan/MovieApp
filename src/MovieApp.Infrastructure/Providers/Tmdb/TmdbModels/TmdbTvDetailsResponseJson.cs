namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTvDetailsResponseJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? OriginalName { get; set; }

    public string? Overview { get; set; }

    public string? FirstAirDate { get; set; }

    public string? LastAirDate { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public string? OriginalLanguage { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public string? Status { get; set; }

    public List<TmdbGenreJson> Genres { get; set; } = [];

    public List<TmdbTvSeasonSummaryJson> Seasons { get; set; } = [];

    public TmdbExternalIdsJson? ExternalIds { get; set; }
}

internal sealed class TmdbTvSeasonSummaryJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int SeasonNumber { get; set; }

    public int? EpisodeCount { get; set; }

    public string? AirDate { get; set; }

    public string? PosterPath { get; set; }
}
