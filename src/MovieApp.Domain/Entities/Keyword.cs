using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class Keyword
{
    public Guid Id { get; set; }

    public int? TmdbKeywordId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? CanonicalName { get; set; }

    public string? NormalizedName { get; set; }

    public KeywordSemanticCategory SemanticCategory { get; set; }

    public KeywordClassificationStatus ClassificationStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<MovieKeyword> MovieKeywords { get; set; } = [];

    public ICollection<TvShowKeyword> TvShowKeywords { get; set; } = [];

    public ICollection<KeywordExternalReference> ExternalReferences { get; set; } = [];

    public ICollection<MovieKeywordSource> MovieKeywordSources { get; set; } = [];

    public ICollection<TvShowKeywordSource> TvShowKeywordSources { get; set; } = [];

    public ICollection<KeywordLocalization> Localizations { get; set; } = [];

    public KeywordDisplayProfile? DisplayProfile { get; set; }
}
