namespace MovieApp.Domain.Entities;

public sealed class Keyword
{
    public Guid Id { get; set; }

    public int TmdbKeywordId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<MovieKeyword> MovieKeywords { get; set; } = [];

    public ICollection<TvShowKeyword> TvShowKeywords { get; set; } = [];
}
