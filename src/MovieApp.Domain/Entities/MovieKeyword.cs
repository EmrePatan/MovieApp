namespace MovieApp.Domain.Entities;

public sealed class MovieKeyword
{
    public Guid MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    public Guid KeywordId { get; set; }

    public Keyword Keyword { get; set; } = null!;
}
