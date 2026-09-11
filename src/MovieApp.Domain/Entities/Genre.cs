namespace MovieApp.Domain.Entities;

public sealed class Genre
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<MovieGenre> MovieGenres { get; set; } = [];

    public ICollection<TvShowGenre> TvShowGenres { get; set; } = [];
}
