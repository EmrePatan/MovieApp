using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class KeywordLocalization
{
    public Guid KeywordId { get; set; }

    public string Locale { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public KeywordTranslationSource TranslationSource { get; set; } = KeywordTranslationSource.Machine;

    public KeywordTranslationReviewStatus ReviewStatus { get; set; } = KeywordTranslationReviewStatus.Unreviewed;

    public string SourceTextHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Keyword Keyword { get; set; } = null!;
}