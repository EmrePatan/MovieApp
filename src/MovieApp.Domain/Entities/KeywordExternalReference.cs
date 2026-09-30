using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class KeywordExternalReference
{
    public Guid KeywordId { get; set; }

    public Keyword Keyword { get; set; } = null!;

    public KeywordProvider Provider { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string? ExternalName { get; set; }

    public DateTime CreatedAt { get; set; }
}
