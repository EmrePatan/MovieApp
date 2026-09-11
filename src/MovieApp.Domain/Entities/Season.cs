namespace MovieApp.Domain.Entities;

public sealed class Season
{
    public Guid Id { get; set; }

    public Guid TvShowId { get; set; }

    public TvShow TvShow { get; set; } = null!;

    public int? TmdbId { get; set; }

    public int? TvdbId { get; set; }

    public int SeasonNumber { get; set; }

    public string? Name { get; set; }

    public string? Overview { get; set; }

    public DateOnly? AirDate { get; set; }

    public int? EpisodeCount { get; set; }

    public string? PosterPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<Episode> Episodes { get; set; } = [];
}
