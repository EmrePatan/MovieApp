namespace MovieApp.Domain.Entities;

public sealed class Episode
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Season Season { get; set; } = null!;

    public int? TmdbId { get; set; }

    public int? TvdbId { get; set; }

    public string? ImdbId { get; set; }

    public int EpisodeNumber { get; set; }

    public string? Name { get; set; }

    public string? Overview { get; set; }

    public DateOnly? AirDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public string? StillPath { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<WatchedEpisode> WatchedEpisodes { get; set; } = [];
}
