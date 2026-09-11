namespace MovieApp.Domain.Entities;

public sealed class Person
{
    public Guid Id { get; set; }

    public int? TmdbId { get; set; }

    public int? TvdbId { get; set; }

    public string? ImdbId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ProfilePath { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<MoviePerson> MoviePeople { get; set; } = [];

    public ICollection<TvShowPerson> TvShowPeople { get; set; } = [];
}
