namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

internal sealed class TmdbTvSeasonDetailsResponseJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Overview { get; set; }

    public string? AirDate { get; set; }

    public int? EpisodeCount { get; set; }

    public string? PosterPath { get; set; }

    public int SeasonNumber { get; set; }

    public List<TmdbTvEpisodeJson> Episodes { get; set; } = [];
}

internal sealed class TmdbTvEpisodeJson
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? Overview { get; set; }

    public string? AirDate { get; set; }

    public int EpisodeNumber { get; set; }

    public int SeasonNumber { get; set; }

    public int? Runtime { get; set; }

    public string? StillPath { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public TmdbExternalIdsJson? ExternalIds { get; set; }
}
