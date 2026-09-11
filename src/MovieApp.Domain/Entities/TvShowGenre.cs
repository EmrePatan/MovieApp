namespace MovieApp.Domain.Entities;

public sealed class TvShowGenre
{
    public Guid TvShowId { get; set; }

    public TvShow TvShow { get; set; } = null!;

    public Guid GenreId { get; set; }

    public Genre Genre { get; set; } = null!;
}
