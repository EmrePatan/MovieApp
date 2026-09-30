using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class TvShowKeywordSource
{
    public Guid TvShowId { get; set; }

    public TvShow TvShow { get; set; } = null!;

    public Guid KeywordId { get; set; }

    public Keyword Keyword { get; set; } = null!;

    public KeywordProvider Provider { get; set; }

    public DateTime? FirstSeenAtUtc { get; set; }

    public DateTime? LastSeenAtUtc { get; set; }
}
