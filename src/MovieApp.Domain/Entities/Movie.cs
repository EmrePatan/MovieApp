namespace MovieApp.Domain.Entities;

public sealed class Movie
{
    public Guid Id { get; set; }

    public int? TmdbId { get; set; }

    public int? TvdbId { get; set; }

    public string? ImdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? OriginalTitle { get; set; }

    public string? Overview { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public int? RuntimeMinutes { get; set; }

    public string? PosterPath { get; set; }

    public string? BackdropPath { get; set; }

    public string? OriginalLanguage { get; set; }

    public string? PrimaryOriginCountryCode { get; set; }

    public decimal VoteAverage { get; set; }

    public int VoteCount { get; set; }

    public int? TmdbCollectionId { get; set; }

    public string? CollectionName { get; set; }

    public string? CollectionPosterPath { get; set; }

    public string? CollectionBackdropPath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? KeywordsSyncedAtUtc { get; set; }

    public DateTime? MdbListKeywordsSyncedAtUtc { get; set; }

    /// <summary>
    /// When full TMDB detail metadata was last successfully applied via provider upsert.
    /// </summary>
    public DateTime? TmdbMetadataUpdatedAtUtc { get; set; }

    /// <summary>
    /// When the title last appeared on a provider weekly-trending snapshot ingest (discovery signal).
    /// </summary>
    public DateTime? TmdbProviderDiscoverySeenAtUtc { get; set; }

    public ICollection<MovieGenre> MovieGenres { get; set; } = [];

    public ICollection<MovieKeyword> MovieKeywords { get; set; } = [];

    public ICollection<MoviePerson> MoviePeople { get; set; } = [];

    public ICollection<Favorite> Favorites { get; set; } = [];

    public ICollection<WatchlistItem> WatchlistItems { get; set; } = [];

    public ICollection<Rating> Ratings { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public ICollection<WatchedMovie> WatchedMovies { get; set; } = [];

    public ICollection<CatalogReleaseEvent> CatalogReleaseEvents { get; set; } = [];

    public ICollection<MovieRegionalRelease> RegionalReleases { get; set; } = [];

    public ICollection<UserReleaseNotification> ReleaseNotifications { get; set; } = [];
}
