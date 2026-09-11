using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class TvShow
{
    public Guid Id { get; set; }

    public int? TmdbId { get; set; }

    public int? TvdbId { get; set; }

    public string? ImdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public DateOnly? FirstAirDate { get; set; }

    public DateOnly? LastAirDate { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public string? OriginalLanguage { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public TvShowStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<TvShowGenre> TvShowGenres { get; set; } = [];

    public ICollection<TvShowPerson> TvShowPeople { get; set; } = [];

    public ICollection<Season> Seasons { get; set; } = [];

    public ICollection<Favorite> Favorites { get; set; } = [];

    public ICollection<WatchlistItem> WatchlistItems { get; set; } = [];

    public ICollection<Rating> Ratings { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];
}
