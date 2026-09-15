namespace MovieApp.Domain.Entities;

public sealed class TvShowKeyword
{
    public Guid TvShowId { get; set; }

    public TvShow TvShow { get; set; } = null!;

    public Guid KeywordId { get; set; }

    public Keyword Keyword { get; set; } = null!;
}
