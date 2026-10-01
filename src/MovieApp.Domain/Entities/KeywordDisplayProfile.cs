namespace MovieApp.Domain.Entities;

public sealed class KeywordDisplayProfile
{
    public Guid KeywordId { get; set; }

    public Keyword Keyword { get; set; } = null!;

    public int DocumentFrequency { get; set; }

    public int MovieTitleCount { get; set; }

    public int TvTitleCount { get; set; }

    public int DisplayRank { get; set; }

    public bool Displayable { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
